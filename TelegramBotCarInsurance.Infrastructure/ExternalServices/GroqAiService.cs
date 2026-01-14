using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TelegramBotCarInsurance.Core.Interfaces;

namespace TelegramBotCarInsurance.Infrastructure.ExternalServices
{
    public class GroqAiService : IAiAssistant
    {
        private readonly HttpClient _http;
        private readonly GroqOptions _opt;

        public GroqAiService(HttpClient http, IOptions<GroqOptions> options)
        {
            _http = http;
            _opt = options.Value;

            if (string.IsNullOrWhiteSpace(_opt.ApiKey))
                throw new InvalidOperationException("Groq API key is missing.");

            _http.BaseAddress = new Uri("https://api.groq.com/openai/");
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _opt.ApiKey);
        }

        public async Task<string> GenerateReplyAsync(string systemPrompt, string userMessage)
        {
            var payload = new
            {
                model = _opt.Model,
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userMessage }
                },
                temperature = 0.3
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var resp = await _http.PostAsync("v1/chat/completions", content);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"Groq error {(int)resp.StatusCode}: {body}");

            return ExtractAssistantText(body) ?? string.Empty;
        }

        public async Task<string> GeneratePolicyDocumentAsync(string name, string car, string plate)
        {
            var system = """
                You generate a dummy car insurance policy document.

                HARD RULES:
                - Output ONLY the policy text.
                - NO explanations, NO reasoning, NO markdown.
                - Use English language only.
                - Keep it short, clear, and official.

                EXACT FORMAT:

                === CAR INSURANCE POLICY #<POLICY_NUMBER> ===
                Policyholder: <NAME>
                Vehicle: <CAR>
                License Plate: <PLATE>
                Amount: 100 USD
                Status: PAID
                Date: <YYYY-MM-DD>
                ==========================================
                """;

            var user = $"""
                NAME: {name}
                CAR: {car}
                PLATE: {plate}
                DATE: {DateTime.UtcNow:yyyy-MM-dd}
                POLICY_NUMBER: {Random.Shared.Next(10000, 99999)}
                """;

            var raw = await GenerateReplyAsync(system, user);
            return StripToPolicy(raw);
        }

        private static string? ExtractAssistantText(string json)
        {
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("choices", out var choices) ||
                choices.ValueKind != JsonValueKind.Array ||
                choices.GetArrayLength() == 0)
                return null;

            var msg = choices[0].GetProperty("message");
            return msg.TryGetProperty("content", out var content)
                ? content.GetString()
                : null;
        }

        private static string StripToPolicy(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            var idx = text.IndexOf("=== CAR INSURANCE POLICY", StringComparison.OrdinalIgnoreCase);
            return idx >= 0 ? text.Substring(idx).Trim() : text.Trim();
        }
    }
}
