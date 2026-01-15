namespace TelegramBotCarInsurance.Infrastructure.ExternalServices
{
    /// <summary>
    /// Configuration options for the Groq AI service.
    /// Values are typically provided via appsettings.json
    /// and bound using the Options pattern.
    /// </summary>
    public sealed class GroqOptions
    {
        /// <summary>
        /// API key used to authenticate requests to the Groq API.
        /// This value should NOT be committed to source control.
        /// </summary>
        public string ApiKey { get; set; } = "";

        /// <summary>
        /// Identifier of the Groq model to use for chat completions.
        /// Defaults to a generic compound model suitable for conversations.
        /// </summary>
        public string Model { get; set; } = "groq/compound";
    }
}
