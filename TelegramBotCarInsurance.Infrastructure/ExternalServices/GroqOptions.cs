namespace TelegramBotCarInsurance.Infrastructure.ExternalServices
{
    public sealed class GroqOptions
    {
        public string ApiKey { get; set; } = "";
        public string Model { get; set; } = "groq/compound";
    }
}
