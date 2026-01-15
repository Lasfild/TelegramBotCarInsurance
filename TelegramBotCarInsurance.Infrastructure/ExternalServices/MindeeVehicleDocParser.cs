using System.Text.Json;
using Microsoft.Extensions.Options;
using TelegramBotCarInsurance.Core.Models;

namespace TelegramBotCarInsurance.Infrastructure.ExternalServices
{
    /// <summary>
    /// Mindee-based parser for vehicle registration documents.
    /// 
    /// This parser uses a dedicated Mindee custom extraction model
    /// to extract only the fields required for insurance issuance:
    /// - vehicle brand / model
    /// - license plate number
    /// 
    /// Implements <see cref="TelegramBotCarInsurance.Core.Interfaces.IDocumentParser"/>
    /// to integrate seamlessly with the application workflow.
    /// </summary>
    // Explicit interface implementation is used to avoid potential namespace mismatch issues
    public class MindeeVehicleDocParser
        : MindeeBaseParser,
          TelegramBotCarInsurance.Core.Interfaces.IDocumentParser
    {
        // HTTP client used to communicate with Mindee API
        private readonly HttpClient _http;

        // Strongly typed configuration options for the vehicle document model
        private readonly MindeeVehicleOptions _opt;

        /// <summary>
        /// Constructor.
        /// Validates required configuration and prepares the HTTP client.
        /// </summary>
        public MindeeVehicleDocParser(HttpClient http, IOptions<MindeeVehicleOptions> options)
        {
            _http = http;
            _opt = options.Value;

            // Ensure API key is configured
            if (string.IsNullOrWhiteSpace(_opt.ApiKey))
                throw new InvalidOperationException("MindeeVehicle:ApiKey is missing.");

            // Ensure model ID is configured
            if (string.IsNullOrWhiteSpace(_opt.ModelId))
                throw new InvalidOperationException("MindeeVehicle:ModelId is missing.");

            // Ensure Mindee v2 base address is set
            _http.BaseAddress = new Uri("https://api-v2.mindee.net/");
        }

        /// <summary>
        /// Extracts vehicle data from the provided image stream.
        /// </summary>
        /// <param name="imageStream">
        /// Image stream containing the vehicle registration document photo
        /// (typically downloaded from Telegram servers).
        /// </param>
        /// <returns>
        /// A <see cref="UserSession"/> instance populated with vehicle-related fields.
        /// Only vehicle fields are filled; passport fields remain null.
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

            // Safely navigate to inference.result.fields
            if (!TryGet(doc.RootElement, out var fields, "inference", "result", "fields"))
                return new UserSession();

            // =========================
            // FIELD EXTRACTION STRATEGY
            // =========================
            //
            // ONLY REQUIRED FIELDS:
            // - License plate: usually field "a" (registration number)
            // - Vehicle model: usually "d3" (commercial description/model)
            // - Vehicle brand: usually "d1"
            //
            // Field naming depends on how the Mindee model was trained,
            // therefore multiple fallbacks are applied.

            // Primary license plate field
            var plate = ReadFieldValue(fields, "a");

            // Primary brand and model fields
            var brand = ReadFieldValue(fields, "d1");   // brand / make
            var model = ReadFieldValue(fields, "d3");   // model / commercial description

            // Fallbacks in case primary fields are not present
            model ??= ReadFieldValue(fields, "d2_1");   // type / variant / version
            brand ??= ReadFieldValue(fields, "d2");     // alternative brand/type field

            // Worst-case fallback for license plate
            // (depends on how the custom model was labeled)
            plate ??= ReadFieldValue(fields, "document_number");

            // Build final car model string (e.g., "Volkswagen Golf")
            var carModel = BuildCarModel(brand, model);

            // Return session populated only with vehicle data
            return new UserSession
            {
                CarModel = carModel,
                LicensePlate = NormalizePlate(plate)
            };
        }

        /// <summary>
        /// Combines brand and model into a single human-readable vehicle name.
        /// </summary>
        private static string? BuildCarModel(string? brand, string? model)
        {
            brand = Clean(brand);
            model = Clean(model);

            if (!string.IsNullOrWhiteSpace(brand) && !string.IsNullOrWhiteSpace(model))
                return $"{brand} {model}".Trim();

            return brand ?? model;
        }

        /// <summary>
        /// Normalizes license plate value by trimming and removing spaces.
        /// </summary>
        private static string? NormalizePlate(string? plate)
        {
            plate = Clean(plate);
            if (string.IsNullOrWhiteSpace(plate))
                return null;

            // Remove spaces just in case they appear in OCR output
            return plate.Replace(" ", "").Trim();
        }

        /// <summary>
        /// Cleans a string value by trimming whitespace
        /// and converting empty strings to null.
        /// </summary>
        private static string? Clean(string? s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return null;

            return s.Trim();
        }
    }
}
