using System.Text.Json;
using Microsoft.Extensions.Options;
using TelegramBotCarInsurance.Core.Models;

namespace TelegramBotCarInsurance.Infrastructure.ExternalServices
{
    /// <summary>
    /// Mindee-based parser for passport documents.
    /// 
    /// This parser uses a dedicated Mindee custom extraction model
    /// to extract only the required passport fields:
    /// - given names
    /// - surnames
    /// - document number
    /// 
    /// Implements <see cref="TelegramBotCarInsurance.Core.Interfaces.IDocumentParser"/>
    /// so it can be used transparently by the application layer.
    /// </summary>
    // EXPLICIT fully-qualified interface implementation is used
    // to avoid potential namespace ambiguity.
    public class MindeePassportParser
        : MindeeBaseParser,
          TelegramBotCarInsurance.Core.Interfaces.IDocumentParser
    {
        // HTTP client used for Mindee API calls
        private readonly HttpClient _http;

        // Strongly typed options specific to passport recognition
        private readonly MindeePassportOptions _opt;

        /// <summary>
        /// Constructor.
        /// Validates required configuration and prepares the HTTP client.
        /// </summary>
        public MindeePassportParser(HttpClient http, IOptions<MindeePassportOptions> options)
        {
            _http = http;
            _opt = options.Value;

            // Ensure API key is configured
            if (string.IsNullOrWhiteSpace(_opt.ApiKey))
                throw new InvalidOperationException("MindeePassport:ApiKey is missing.");

            // Ensure model ID is configured
            if (string.IsNullOrWhiteSpace(_opt.ModelId))
                throw new InvalidOperationException("MindeePassport:ModelId is missing.");

            // Ensure Mindee v2 base address is set
            _http.BaseAddress = new Uri("https://api-v2.mindee.net/");
        }

        /// <summary>
        /// Extracts passport data from the provided image stream.
        /// </summary>
        /// <param name="imageStream">
        /// Image stream containing the passport photo
        /// (typically downloaded from Telegram servers).
        /// </param>
        /// <returns>
        /// A <see cref="UserSession"/> instance populated with passport-related fields.
        /// Only passport fields are filled; other fields remain null.
        /// </returns>
        public async Task<UserSession> ExtractDataAsync(Stream imageStream)
        {
            // Run Mindee inference and obtain raw JSON result
            var resultJson = await RunInferenceAsync(
                _http,
                _opt.ApiKey,
                _opt.ModelId,
                imageStream,
                _opt.InitialDelayMs,
                _opt.PollingDelayMs,
                _opt.MaxPollAttempts);

            using var doc = JsonDocument.Parse(resultJson);

            // Navigate safely to inference.result.fields
            if (!TryGet(doc.RootElement, out var fields, "inference", "result", "fields"))
            {
                // If expected structure is missing, return empty session
                return new UserSession();
            }

            // Map only required fields from Mindee output
            return new UserSession
            {
                GivenNames = ReadFieldValue(fields, "given_names"),
                Surnames = ReadFieldValue(fields, "surnames"),
                DocumentNumber = ReadFieldValue(fields, "document_number")
            };
        }
    }
}
