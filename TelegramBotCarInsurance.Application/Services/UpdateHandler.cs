using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBotCarInsurance.Core.Enums;
using TelegramBotCarInsurance.Core.Interfaces;
using TelegramBotCarInsurance.Core.Models;
using TelegramBotCarInsurance.Infrastructure.ExternalServices;

namespace TelegramBotCarInsurance.Application.Services
{
    /// <summary>
    /// Main application-level handler for Telegram updates.
    /// Implements the bot workflow (state machine):
    /// Start -> Passport -> Confirm Passport -> Vehicle Doc -> Confirm Vehicle -> Price -> Policy -> Completed.
    /// Also supports AI Q&A: if user sends text while bot expects a photo, AI answers and then reminds to send the photo.
    /// </summary>
    public class UpdateHandler
    {
        // Telegram client used to send messages and download files
        private readonly ITelegramBotClient _botClient;

        // Repository that stores per-user session state (in-memory in this project)
        private readonly IUserRepository _userRepo;

        // Mindee parsers: separate parser per document type/model
        private readonly MindeePassportParser _passportParser;
        private readonly MindeeVehicleDocParser _vehicleParser;

        // AI assistant (Groq / OpenAI-compatible) used for Q&A and policy generation
        private readonly IAiAssistant _ai;

        /// <summary>
        /// Constructor receives dependencies via DI.
        /// </summary>
        public UpdateHandler(
            ITelegramBotClient botClient,
            IUserRepository userRepo,
            MindeePassportParser passportParser,
            MindeeVehicleDocParser vehicleParser,
            IAiAssistant ai)
        {
            _botClient = botClient;
            _userRepo = userRepo;
            _passportParser = passportParser;
            _vehicleParser = vehicleParser;
            _ai = ai;
        }

        /// <summary>
        /// Entry point that routes an incoming Telegram update to the correct workflow step.
        /// </summary>
        public async Task HandleUpdateAsync(Update update)
        {
            // We only handle message updates (ignore callback queries, etc.)
            if (update.Message is not { } message) return;

            // Identify chat and load/create session
            var chatId = message.Chat.Id;
            var session = _userRepo.GetOrCreate(chatId);

            // Command: /start resets the workflow to the beginning
            if ((message.Text ?? "").Trim().Equals("/start", StringComparison.OrdinalIgnoreCase))
            {
                Reset(session);
                _userRepo.Update(session);
            }

            // State machine: route to step handler depending on current session state
            switch (session.State)
            {
                case UserState.Start:
                    // Initial greeting + instructions
                    await _botClient.SendMessage(
                        chatId,
                        "Hello! I will help you purchase car insurance.\n" +
                        "Please send a photo of your PASSPORT to get started."
                    );

                    // Move to the next step: waiting for passport photo
                    session.State = UserState.WaitingForPassport;
                    _userRepo.Update(session);
                    break;

                case UserState.WaitingForPassport:
                    // Expecting passport photo (or answer questions if user sends text)
                    await HandleWaitingForPassport(chatId, message, session);
                    break;

                case UserState.ConfirmingPassport:
                    // Expecting Yes/No to confirm extracted passport data
                    await HandlePassportConfirmation(chatId, message, session);
                    break;

                case UserState.WaitingForVehicleDoc:
                    // Expecting vehicle registration document photo (or answer questions if user sends text)
                    await HandleWaitingForVehicleDoc(chatId, message, session);
                    break;

                case UserState.ConfirmingVehicleDoc:
                    // Expecting Yes/No to confirm extracted vehicle data
                    await HandleVehicleConfirmation(chatId, message, session);
                    break;

                case UserState.PriceAgreement:
                    // Expecting Yes/No to confirm fixed price of 100 USD
                    await HandlePrice(chatId, message, session);
                    break;

                case UserState.Completed:
                    // If flow is already completed, offer to restart
                    await _botClient.SendMessage(
                        chatId,
                        "Your insurance policy is already issued.\n" +
                        "Send /start if you want to create a new one."
                    );

                    // Reset back to Start so user can begin new flow
                    session.State = UserState.Start;
                    _userRepo.Update(session);
                    break;
            }
        }

        // =========================
        // WAITING STATES (TEXT => AI, PHOTO => CONTINUE)
        // =========================

        /// <summary>
        /// Passport step:
        /// - If user sends TEXT: answer via AI and remind to send passport photo.
        /// - If user sends PHOTO: run Mindee passport model, show extracted data, and ask for confirmation.
        /// </summary>
        private async Task HandleWaitingForPassport(long chatId, Message message, UserSession session)
        {
            // If user sent text (e.g., "Why do I need to send a photo?")
            // we answer using AI but keep the state unchanged (still waiting for passport).
            if (IsTextWithoutPhoto(message))
            {
                await AnswerUserQuestionAndRemind(
                    chatId,
                    message.Text!,
                    reminder: "Reminder: please send a photo of your PASSPORT to continue."
                );
                return;
            }

            // If user didn't send a photo (and also didn't send text)
            // we prompt them again.
            if (message.Photo == null || message.Photo.Length == 0)
            {
                await _botClient.SendMessage(chatId, "Please send a PHOTO of your passport.");
                return;
            }

            // Photo received => proceed to parse and extract fields
            await _botClient.SendMessage(chatId, "Analyzing passport…");

            // Extract data through Mindee parser (passport model)
            var extracted = await ExtractViaParser(_passportParser, message);

            // Save extracted data into current session
            session.GivenNames = extracted.GivenNames;
            session.Surnames = extracted.Surnames;
            session.DocumentNumber = extracted.DocumentNumber;
            _userRepo.Update(session);

            // Show extracted fields and request confirmation
            await _botClient.SendMessage(
                chatId,
                $"Passport data detected:\n" +
                $"First name(s): {session.GivenNames ?? "(not detected)"}\n" +
                $"Last name: {session.Surnames ?? "(not detected)"}\n" +
                $"Document number: {session.DocumentNumber ?? "(not detected)"}\n\n" +
                $"Is this information correct? (Yes / No)"
            );

            // Move to confirmation state
            session.State = UserState.ConfirmingPassport;
            _userRepo.Update(session);
        }

        /// <summary>
        /// Vehicle document step:
        /// - If user sends TEXT: answer via AI and remind to send vehicle doc photo.
        /// - If user sends PHOTO: run Mindee vehicle model, show extracted data, and ask for confirmation.
        /// </summary>
        private async Task HandleWaitingForVehicleDoc(long chatId, Message message, UserSession session)
        {
            // If user asks a question instead of sending photo, answer and remind
            if (IsTextWithoutPhoto(message))
            {
                await AnswerUserQuestionAndRemind(
                    chatId,
                    message.Text!,
                    reminder: "Reminder: please send a photo of your VEHICLE REGISTRATION DOCUMENT to continue."
                );
                return;
            }

            // No photo provided => re-prompt
            if (message.Photo == null || message.Photo.Length == 0)
            {
                await _botClient.SendMessage(chatId, "Please send a PHOTO of your vehicle registration document.");
                return;
            }

            // Photo received => proceed
            await _botClient.SendMessage(chatId, "Analyzing vehicle document…");

            // Extract vehicle info through Mindee parser (vehicle model)
            var extracted = await ExtractViaParser(_vehicleParser, message);

            // Save extracted vehicle fields into session
            session.CarModel = extracted.CarModel;
            session.LicensePlate = extracted.LicensePlate;
            _userRepo.Update(session);

            // Show extracted fields and request confirmation
            await _botClient.SendMessage(
                chatId,
                $"Vehicle data detected:\n" +
                $"Vehicle model: {session.CarModel ?? "(not detected)"}\n" +
                $"License plate: {session.LicensePlate ?? "(not detected)"}\n\n" +
                $"Is this information correct? (Yes / No)"
            );

            // Move to confirmation state
            session.State = UserState.ConfirmingVehicleDoc;
            _userRepo.Update(session);
        }

        /// <summary>
        /// Uses AI assistant to answer user's text question and then sends a reminder message.
        /// This is used when the bot is waiting for a document photo but the user sends a text message.
        /// </summary>
        private async Task AnswerUserQuestionAndRemind(long chatId, string userText, string reminder)
        {
            // System prompt defines how AI should behave inside the bot context.
            // Important constraints:
            // - answer briefly
            // - do not ask for sensitive data
            // - do not mention internal APIs
            var systemPrompt =
                "You are an assistant inside a Telegram car insurance bot. " +
                "Answer the user's question briefly and clearly. " +
                "Do not mention internal APIs. Do not ask for sensitive data. " +
                "After answering, do NOT add extra steps—just answer the question.";

            string aiReply;
            try
            {
                // Ask AI to generate a response
                aiReply = await _ai.GenerateReplyAsync(systemPrompt, userText);
                aiReply = (aiReply ?? string.Empty).Trim();
            }
            catch
            {
                // If AI fails (network, rate limit, etc.), do not break the flow
                aiReply = "Sorry, I couldn't answer right now. Please continue with the required step.";
            }

            // Send AI response if available
            if (!string.IsNullOrWhiteSpace(aiReply))
                await _botClient.SendMessage(chatId, aiReply);

            // Always send a reminder to keep the flow clear for user
            await _botClient.SendMessage(chatId, reminder);
        }

        /// <summary>
        /// Helper: returns true when user sends a text message but no photo.
        /// Used to trigger "AI Q&A" behavior.
        /// </summary>
        private static bool IsTextWithoutPhoto(Message message)
            => !string.IsNullOrWhiteSpace(message.Text) && (message.Photo == null || message.Photo.Length == 0);

        // =========================
        // CONFIRMATION + PRICE
        // =========================

        /// <summary>
        /// Handles user confirmation of extracted passport data.
        /// - Yes: proceed to vehicle document step
        /// - No: reset passport data and ask for a new passport photo
        /// </summary>
        private async Task HandlePassportConfirmation(long chatId, Message message, UserSession session)
        {
            var text = (message.Text ?? "").Trim().ToLowerInvariant();

            if (IsYes(text))
            {
                // User confirmed passport => request vehicle document
                await _botClient.SendMessage(
                    chatId,
                    "Great! Now please send a photo of your VEHICLE REGISTRATION DOCUMENT."
                );

                session.State = UserState.WaitingForVehicleDoc;
                _userRepo.Update(session);
                return;
            }

            if (IsNo(text))
            {
                // User rejected extracted passport data => clear it and ask for a re-take
                session.GivenNames = null;
                session.Surnames = null;
                session.DocumentNumber = null;

                session.State = UserState.WaitingForPassport;
                _userRepo.Update(session);

                await _botClient.SendMessage(chatId, "Okay, please send a clearer photo of your passport.");
                return;
            }

            // Any other response => re-prompt for Yes/No
            await _botClient.SendMessage(chatId, "Please answer with Yes or No.");
        }

        /// <summary>
        /// Handles user confirmation of extracted vehicle data.
        /// - Yes: proceed to fixed price confirmation
        /// - No: reset vehicle data and ask for a new vehicle document photo
        /// </summary>
        private async Task HandleVehicleConfirmation(long chatId, Message message, UserSession session)
        {
            var text = (message.Text ?? "").Trim().ToLowerInvariant();

            if (IsYes(text))
            {
                // Vehicle data confirmed => show fixed price and ask for agreement
                await _botClient.SendMessage(
                    chatId,
                    "The insurance price is fixed at 100 USD.\nDo you agree? (Yes / No)"
                );

                session.State = UserState.PriceAgreement;
                _userRepo.Update(session);
                return;
            }

            if (IsNo(text))
            {
                // Vehicle data rejected => clear it and ask for re-take
                session.CarModel = null;
                session.LicensePlate = null;

                session.State = UserState.WaitingForVehicleDoc;
                _userRepo.Update(session);

                await _botClient.SendMessage(chatId, "Okay, please send a clearer photo of your vehicle document.");
                return;
            }

            // Any other response => re-prompt for Yes/No
            await _botClient.SendMessage(chatId, "Please answer with Yes or No.");
        }

        /// <summary>
        /// Handles fixed price agreement step.
        /// - Yes: generate and send insurance policy
        /// - No: apologize and explain price is fixed
        /// </summary>
        private async Task HandlePrice(long chatId, Message message, UserSession session)
        {
            var text = (message.Text ?? "").Trim().ToLowerInvariant();

            if (IsYes(text))
            {
                // User agreed to the price => issue policy
                await _botClient.SendMessage(chatId, "Issuing your insurance policy…");

                // Combine name fields extracted from passport
                var fullName = $"{session.GivenNames} {session.Surnames}".Trim();

                // Generate policy text using AI assistant
                var policyText = await _ai.GeneratePolicyDocumentAsync(
                    fullName,
                    session.CarModel ?? "Unknown",
                    session.LicensePlate ?? "Unknown"
                );

                // Send policy and final confirmation to user
                await _botClient.SendMessage(chatId, policyText);
                await _botClient.SendMessage(chatId, "Thank you for your purchase!");

                // Mark flow as completed
                session.State = UserState.Completed;
                _userRepo.Update(session);
                return;
            }

            if (IsNo(text))
            {
                // User rejected the price => explain price is fixed
                await _botClient.SendMessage(
                    chatId,
                    "Sorry, the price of 100 USD is final and cannot be changed."
                );
                return;
            }

            // Any other response => re-prompt for Yes/No
            await _botClient.SendMessage(chatId, "Please answer with Yes or No.");
        }

        // =========================
        // HELPERS
        // =========================

        /// <summary>
        /// Downloads Telegram photo into a MemoryStream and passes it to the selected parser.
        /// </summary>
        private async Task<UserSession> ExtractViaParser(
            TelegramBotCarInsurance.Core.Interfaces.IDocumentParser parser,
            Message message)
        {
            // Take the highest resolution photo (last item)
            var fileId = message.Photo!.Last().FileId;

            // Request file metadata (path on Telegram servers)
            var fileInfo = await _botClient.GetFile(fileId);

            // Download file into memory
            using var memoryStream = new MemoryStream();
            await _botClient.DownloadFile(fileInfo.FilePath!, memoryStream);

            // Reset stream position before passing to parser
            memoryStream.Position = 0;

            // Parse and extract fields from image stream
            return await parser.ExtractDataAsync(memoryStream);
        }

        /// <summary>
        /// Accepts simple Yes variants.
        /// </summary>
        private static bool IsYes(string text)
            => text is "yes" or "y" or "ok" or "okay";

        /// <summary>
        /// Accepts simple No variants.
        /// </summary>
        private static bool IsNo(string text)
            => text is "no" or "n";

        /// <summary>
        /// Resets session to initial values so user can restart the flow.
        /// </summary>
        private static void Reset(UserSession s)
        {
            s.State = UserState.Start;
            s.GivenNames = null;
            s.Surnames = null;
            s.DocumentNumber = null;
            s.CarModel = null;
            s.LicensePlate = null;
        }
    }
}
