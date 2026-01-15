namespace TelegramBotCarInsurance.Core.Interfaces
{
    /// <summary>
    /// Abstraction for AI-based assistant used by the bot.
    /// Responsible for:
    /// - answering user questions in free text form
    /// - generating a dummy insurance policy document
    /// 
    /// This interface allows easy replacement of the AI provider
    /// (e.g., Groq, OpenAI, mock implementation).
    /// </summary>
    public interface IAiAssistant
    {
        /// <summary>
        /// Generates a textual response to a user's question.
        /// Used when the user sends a text message instead of the expected document/photo.
        /// </summary>
        /// <param name="systemPrompt">
        /// Instruction that defines the assistant's behavior and context
        /// (e.g., role, restrictions, response style).
        /// </param>
        /// <param name="userMessage">
        /// Raw text message sent by the user.
        /// </param>
        /// <returns>
        /// AI-generated response text.
        /// </returns>
        Task<string> GenerateReplyAsync(string systemPrompt, string userMessage);

        /// <summary>
        /// Generates a dummy car insurance policy document.
        /// The output is expected to be a plain text document
        /// in a predefined, fixed format.
        /// </summary>
        /// <param name="name">Policyholder full name.</param>
        /// <param name="car">Vehicle model or description.</param>
        /// <param name="plate">Vehicle license plate number.</param>
        /// <returns>
        /// Generated insurance policy document as plain text.
        /// </returns>
        Task<string> GeneratePolicyDocumentAsync(string name, string car, string plate);
    }
}
