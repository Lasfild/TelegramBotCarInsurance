namespace TelegramBotCarInsurance.Infrastructure.ExternalServices
{
    public sealed class MindeePassportOptions
    {
        public string ApiKey { get; set; } = string.Empty;
        public string ModelId { get; set; } = string.Empty;

        public int InitialDelayMs { get; set; } = 3000;
        public int PollingDelayMs { get; set; } = 1200;
        public int MaxPollAttempts { get; set; } = 40;
    }
}
