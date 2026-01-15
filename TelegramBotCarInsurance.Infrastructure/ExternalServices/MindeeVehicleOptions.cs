namespace TelegramBotCarInsurance.Infrastructure.ExternalServices
{
    /// <summary>
    /// Configuration options for the Mindee vehicle registration document parser.
    /// These settings define authentication, model selection,
    /// and polling behavior for Mindee V2 inference jobs.
    /// </summary>
    public sealed class MindeeVehicleOptions
    {
        /// <summary>
        /// Mindee API key used to authenticate requests.
        /// This value should be provided via configuration
        /// and must not be committed to source control.
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Identifier of the Mindee custom extraction model
        /// used for vehicle registration document recognition.
        /// </summary>
        public string ModelId { get; set; } = string.Empty;

        /// <summary>
        /// Delay (in milliseconds) before the first polling attempt
        /// after the inference job has been enqueued.
        /// </summary>
        public int InitialDelayMs { get; set; } = 3000;

        /// <summary>
        /// Delay (in milliseconds) between consecutive polling attempts
        /// while waiting for the inference result.
        /// </summary>
        public int PollingDelayMs { get; set; } = 1200;

        /// <summary>
        /// Maximum number of polling attempts before timing out
        /// if the inference result is not returned.
        /// </summary>
        public int MaxPollAttempts { get; set; } = 40;
    }
}
