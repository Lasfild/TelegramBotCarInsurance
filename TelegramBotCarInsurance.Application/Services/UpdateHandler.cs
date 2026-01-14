using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBotCarInsurance.Core.Enums;
using TelegramBotCarInsurance.Core.Interfaces;
using TelegramBotCarInsurance.Core.Models;
using TelegramBotCarInsurance.Infrastructure.ExternalServices;

namespace TelegramBotCarInsurance.Application.Services
{
    public class UpdateHandler
    {
        private readonly ITelegramBotClient _botClient;
        private readonly IUserRepository _userRepo;
        private readonly MindeePassportParser _passportParser;
        private readonly MindeeVehicleDocParser _vehicleParser;
        private readonly IAiAssistant _ai;

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

        public async Task HandleUpdateAsync(Update update)
        {
            if (update.Message is not { } message) return;

            var chatId = message.Chat.Id;
            var session = _userRepo.GetOrCreate(chatId);

            if ((message.Text ?? "").Trim().Equals("/start", StringComparison.OrdinalIgnoreCase))
            {
                Reset(session);
                _userRepo.Update(session);
            }

            switch (session.State)
            {
                case UserState.Start:
                    await _botClient.SendMessage(
                        chatId,
                        "Hello! I will help you purchase car insurance.\n" +
                        "Please send a photo of your PASSPORT to get started."
                    );
                    session.State = UserState.WaitingForPassport;
                    _userRepo.Update(session);
                    break;

                case UserState.WaitingForPassport:
                    await HandlePassportPhoto(chatId, message, session);
                    break;

                case UserState.ConfirmingPassport:
                    await HandlePassportConfirmation(chatId, message, session);
                    break;

                case UserState.WaitingForVehicleDoc:
                    await HandleVehiclePhoto(chatId, message, session);
                    break;

                case UserState.ConfirmingVehicleDoc:
                    await HandleVehicleConfirmation(chatId, message, session);
                    break;

                case UserState.PriceAgreement:
                    await HandlePrice(chatId, message, session);
                    break;

                case UserState.Completed:
                    await _botClient.SendMessage(
                        chatId,
                        "Your insurance policy is already issued.\n" +
                        "Send /start if you want to create a new one."
                    );
                    session.State = UserState.Start;
                    _userRepo.Update(session);
                    break;
            }
        }

        private async Task HandlePassportPhoto(long chatId, Message message, UserSession session)
        {
            if (message.Photo == null || message.Photo.Length == 0)
            {
                await _botClient.SendMessage(chatId, "Please send a PHOTO of your passport.");
                return;
            }

            await _botClient.SendMessage(chatId, "Analyzing passport…");

            var extracted = await ExtractViaParser(_passportParser, message);

            session.GivenNames = extracted.GivenNames;
            session.Surnames = extracted.Surnames;
            session.DocumentNumber = extracted.DocumentNumber;
            _userRepo.Update(session);

            await _botClient.SendMessage(
                chatId,
                $"Passport data detected:\n" +
                $"First name(s): {session.GivenNames ?? "(not detected)"}\n" +
                $"Last name: {session.Surnames ?? "(not detected)"}\n" +
                $"Document number: {session.DocumentNumber ?? "(not detected)"}\n\n" +
                $"Is this information correct? (Yes / No)"
            );

            session.State = UserState.ConfirmingPassport;
            _userRepo.Update(session);
        }

        private async Task HandlePassportConfirmation(long chatId, Message message, UserSession session)
        {
            var text = (message.Text ?? "").Trim().ToLowerInvariant();

            if (IsYes(text))
            {
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
                session.GivenNames = null;
                session.Surnames = null;
                session.DocumentNumber = null;
                session.State = UserState.WaitingForPassport;
                _userRepo.Update(session);

                await _botClient.SendMessage(
                    chatId,
                    "Okay, please send a clearer photo of your passport."
                );
                return;
            }

            await _botClient.SendMessage(chatId, "Please answer with Yes or No.");
        }

        private async Task HandleVehiclePhoto(long chatId, Message message, UserSession session)
        {
            if (message.Photo == null || message.Photo.Length == 0)
            {
                await _botClient.SendMessage(
                    chatId,
                    "Please send a PHOTO of your vehicle registration document."
                );
                return;
            }

            await _botClient.SendMessage(chatId, "Analyzing vehicle document…");

            var extracted = await ExtractViaParser(_vehicleParser, message);

            session.CarModel = extracted.CarModel;
            session.LicensePlate = extracted.LicensePlate;
            _userRepo.Update(session);

            await _botClient.SendMessage(
                chatId,
                $"Vehicle data detected:\n" +
                $"Vehicle model: {session.CarModel ?? "(not detected)"}\n" +
                $"License plate: {session.LicensePlate ?? "(not detected)"}\n\n" +
                $"Is this information correct? (Yes / No)"
            );

            session.State = UserState.ConfirmingVehicleDoc;
            _userRepo.Update(session);
        }

        private async Task HandleVehicleConfirmation(long chatId, Message message, UserSession session)
        {
            var text = (message.Text ?? "").Trim().ToLowerInvariant();

            if (IsYes(text))
            {
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
                session.CarModel = null;
                session.LicensePlate = null;
                session.State = UserState.WaitingForVehicleDoc;
                _userRepo.Update(session);

                await _botClient.SendMessage(
                    chatId,
                    "Okay, please send a clearer photo of your vehicle document."
                );
                return;
            }

            await _botClient.SendMessage(chatId, "Please answer with Yes or No.");
        }

        private async Task HandlePrice(long chatId, Message message, UserSession session)
        {
            var text = (message.Text ?? "").Trim().ToLowerInvariant();

            if (IsYes(text))
            {
                await _botClient.SendMessage(chatId, "Issuing your insurance policy…");

                var fullName = $"{session.GivenNames} {session.Surnames}".Trim();

                var policyText = await _ai.GeneratePolicyDocumentAsync(
                    fullName,
                    session.CarModel ?? "Unknown",
                    session.LicensePlate ?? "Unknown"
                );

                await _botClient.SendMessage(chatId, policyText);
                await _botClient.SendMessage(chatId, "Thank you for your purchase!");

                session.State = UserState.Completed;
                _userRepo.Update(session);
                return;
            }

            if (IsNo(text))
            {
                await _botClient.SendMessage(
                    chatId,
                    "Sorry, the price of 100 USD is final and cannot be changed."
                );
                return;
            }

            await _botClient.SendMessage(chatId, "Please answer with Yes or No.");
        }

        private async Task<UserSession> ExtractViaParser(
            TelegramBotCarInsurance.Core.Interfaces.IDocumentParser parser,
            Message message)
        {
            var fileId = message.Photo!.Last().FileId;
            var fileInfo = await _botClient.GetFile(fileId);

            using var memoryStream = new MemoryStream();
            await _botClient.DownloadFile(fileInfo.FilePath!, memoryStream);
            memoryStream.Position = 0;

            return await parser.ExtractDataAsync(memoryStream);
        }

        private static bool IsYes(string text)
            => text is "yes" or "y" or "ok" or "okay";

        private static bool IsNo(string text)
            => text is "no" or "n";

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
