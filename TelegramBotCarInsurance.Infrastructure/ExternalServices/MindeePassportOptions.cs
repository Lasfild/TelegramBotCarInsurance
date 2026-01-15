namespace TelegramBotCarInsurance.Infrastructure.ExternalServices
{
    /// <summary>
    /// Configuration options for the Mindee passport document parser.
    /// These settings control authentication, model selection,
    /// and polling behavior for Mindee V2 inference jobs.
    /// </summary>
    public sealed class MindeePassportOptions
    {
        /// <summary>
        /// Mindee API key used to authenticate requests.
        /// This value should be stored in configuration
        /// (e.g., appsettings.json or environment variables)
        /// and must not be committed to source control.
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Identifier of the Mindee custom extraction model
        /// used for passport recognition.
        /// </summary>
        public string ModelId { get; set; } = string.Empty;

        /// <summary>
        /// Delay (in milliseconds) before the first polling attempt
        /// after the inference job has been enqueued.
        /// Mindee typically requires a short warm-up time.
        /// </summary>
        public int InitialDelayMs { get; set; } = 3000;

        /// <summary>
        /// Delay (in milliseconds) between consecutive polling attempts
        /// while waiting for the inference result.
        /// </summary>
        public int PollingDelayMs { get; set; } = 1200;

        /// <summary>
        /// Maximum number of polling attempts before giving up
        /// and throwing a timeout exception.
        /// </summary>
        public int MaxPollAttempts { get; set; } = 40;
    }
}
