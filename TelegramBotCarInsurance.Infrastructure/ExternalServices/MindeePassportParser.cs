using System.Text.Json;
using Microsoft.Extensions.Options;
using TelegramBotCarInsurance.Core.Models;

namespace TelegramBotCarInsurance.Infrastructure.ExternalServices
{
    // EXPLICIT fully-qualified interface implementation to avoid namespace mismatch
    public class MindeePassportParser : MindeeBaseParser, TelegramBotCarInsurance.Core.Interfaces.IDocumentParser
    {
        private readonly HttpClient _http;
        private readonly MindeePassportOptions _opt;

        public MindeePassportParser(HttpClient http, IOptions<MindeePassportOptions> options)
        {
            _http = http;
            _opt = options.Value;

            if (string.IsNullOrWhiteSpace(_opt.ApiKey))
                throw new InvalidOperationException("MindeePassport:ApiKey is missing.");

            if (string.IsNullOrWhiteSpace(_opt.ModelId))
                throw new InvalidOperationException("MindeePassport:ModelId is missing.");

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

            return new UserSession
            {
                GivenNames = ReadFieldValue(fields, "given_names"),
                Surnames = ReadFieldValue(fields, "surnames"),
                DocumentNumber = ReadFieldValue(fields, "document_number")
            };
        }
    }
}
