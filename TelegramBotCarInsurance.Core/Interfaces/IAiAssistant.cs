namespace TelegramBotCarInsurance.Core.Interfaces
{
    public interface IAiAssistant
    {
        Task<string> GenerateReplyAsync(string systemPrompt, string userMessage);

        Task<string> GeneratePolicyDocumentAsync(string name, string car, string plate);
    }
}
