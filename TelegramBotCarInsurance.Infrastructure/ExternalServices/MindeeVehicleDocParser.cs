using System.Text.Json;
using Microsoft.Extensions.Options;
using TelegramBotCarInsurance.Core.Models;

namespace TelegramBotCarInsurance.Infrastructure.ExternalServices
{
    // Explicit interface implementation to avoid namespace mismatch issues
    public class MindeeVehicleDocParser : MindeeBaseParser, TelegramBotCarInsurance.Core.Interfaces.IDocumentParser
    {
        private readonly HttpClient _http;
        private readonly MindeeVehicleOptions _opt;

        public MindeeVehicleDocParser(HttpClient http, IOptions<MindeeVehicleOptions> options)
        {
            _http = http;
            _opt = options.Value;

            if (string.IsNullOrWhiteSpace(_opt.ApiKey))
                throw new InvalidOperationException("MindeeVehicle:ApiKey is missing.");

            if (string.IsNullOrWhiteSpace(_opt.ModelId))
                throw new InvalidOperationException("MindeeVehicle:ModelId is missing.");

            _http.BaseAddress = new Uri("https://api-v2.mindee.net/");
        }

        public async Task<UserSession> ExtractDataAsync(Stream imageStream)
        {
            var resultJson = await RunInferenceAsync(
                _http,
                _opt.ApiKey,
                _opt.ModelId,
                imageStream,
                _opt.InitialDelayMs,
                _opt.PollingDelayMs,
                _opt.MaxPollAttempts);

            using var doc = JsonDocument.Parse(resultJson);

            if (!TryGet(doc.RootElement, out var fields, "inference", "result", "fields"))
                return new UserSession();

            // ONLY NEEDED FIELDS:
            // - License plate: usually field "a" (registration number)
            // - Car model: usually "d3" (commercial description/model), plus optional "d1" brand
            var plate = ReadFieldValue(fields, "a");

            var brand = ReadFieldValue(fields, "d1");   // brand / make
            var model = ReadFieldValue(fields, "d3");   // model / commercial description

            // Fallbacks (depends on how you labeled your model)
            model ??= ReadFieldValue(fields, "d2_1");   // sometimes "type variant version"
            brand ??= ReadFieldValue(fields, "d2");     // type / variant
            plate ??= ReadFieldValue(fields, "document_number"); // worst-case fallback (not ideal)

            var carModel = BuildCarModel(brand, model);

            return new UserSession
            {
                CarModel = carModel,
                LicensePlate = NormalizePlate(plate)
            };
        }

        private static string? BuildCarModel(string? brand, string? model)
        {
            brand = Clean(brand);
            model = Clean(model);

            if (!string.IsNullOrWhiteSpace(brand) && !string.IsNullOrWhiteSpace(model))
                return $"{brand} {model}".Trim();

            return brand ?? model;
        }

        private static string? NormalizePlate(string? plate)
        {
            plate = Clean(plate);
            if (string.IsNullOrWhiteSpace(plate)) return null;

            // remove spaces just in case
            return plate.Replace(" ", "").Trim();
        }

        private static string? Clean(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            return s.Trim();
        }
    }
}
