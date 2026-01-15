using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TelegramBotCarInsurance.Core.Interfaces;

namespace TelegramBotCarInsurance.Infrastructure.ExternalServices
{
    /// <summary>
    /// AI assistant implementation based on the Groq API
    /// (OpenAI-compatible chat/completions endpoint).
    /// 
    /// Responsible for:
    /// - answering free-form user questions
    /// - generating a dummy car insurance policy document
    /// </summary>
    public class GroqAiService : IAiAssistant
    {
        // HTTP client used to communicate with Groq API
        private readonly HttpClient _http;

        // Strongly typed Groq configuration options (API key, model, etc.)
        private readonly GroqOptions _opt;

        /// <summary>
        /// Constructor.
        /// Configures HTTP client and validates presence of API key.
        /// </summary>
        public GroqAiService(HttpClient http, IOptions<GroqOptions> options)
        {
            _http = http;
            _opt = options.Value;

            // Ensure API key is provided
            if (string.IsNullOrWhiteSpace(_opt.ApiKey))
                throw new InvalidOperationException("Groq API key is missing.");

            // Configure base address and authorization header
            _http.BaseAddress = new Uri("https://api.groq.com/openai/");
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _opt.ApiKey);
        }

        /// <summary>
        /// Generates an AI response to a user's free-text message.
        /// Used when the user asks a question instead of sending a document/photo.
        /// </summary>
        public async Task<string> GenerateReplyAsync(string systemPrompt, string userMessage)
        {
            // Prepare request payload according to OpenAI-compatible format
            var payload = new
            {
                model = _opt.Model,
                messages = new object[]
                {
                    // System prompt defines assistant behavior and constraints
                    new { role = "system", content = systemPrompt },

                    // User message contains the raw text from the user
                    new { role = "user", content = userMessage }
                },
                // Lower temperature for more deterministic responses
                temperature = 0.3
            };

            // Serialize payload to JSON
            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Send request to Groq chat completion endpoint
            using var resp = await _http.PostAsync("v1/chat/completions", content);
            var body = await resp.Content.ReadAsStringAsync();

            // Throw exception if Groq returned an error
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"Groq error {(int)resp.StatusCode}: {body}");

            // Extract assistant reply text from response JSON
            return ExtractAssistantText(body) ?? string.Empty;
        }

        /// <summary>
        /// Generates a dummy car insurance policy document using AI.
        /// The output must strictly follow a predefined plain-text format.
        /// </summary>
        public async Task<string> GeneratePolicyDocumentAsync(string name, string car, string plate)
        {
            // System instructions enforce strict formatting and content rules
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

            // User message provides dynamic values for the policy template
            var user = $"""
                NAME: {name}
                CAR: {car}
                PLATE: {plate}
                DATE: {DateTime.UtcNow:yyyy-MM-dd}
                POLICY_NUMBER: {Random.Shared.Next(10000, 99999)}
                """;

            // Generate raw AI output
            var raw = await GenerateReplyAsync(system, user);

            // Strip any unwanted text and return only the policy body
            return StripToPolicy(raw);
        }

        /// <summary>
        /// Extracts the assistant message content from the Groq API JSON response.
        /// </summary>
        private static string? ExtractAssistantText(string json)
        {
            using var doc = JsonDocument.Parse(json);

            // Validate expected response structure
            if (!doc.RootElement.TryGetProperty("choices", out var choices) ||
                choices.ValueKind != JsonValueKind.Array ||
                choices.GetArrayLength() == 0)
                return null;

            var msg = choices[0].GetProperty("message");

            // Extract text content from the assistant message
            return msg.TryGetProperty("content", out var content)
                ? content.GetString()
                : null;
        }

        /// <summary>
        /// Ensures that only the insurance policy text is returned.
        /// If extra text is present, everything before the policy header is removed.
        /// </summary>
        private static string StripToPolicy(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            // Locate policy header and trim everything before it
            var idx = text.IndexOf(
                "=== CAR INSURANCE POLICY",
                StringComparison.OrdinalIgnoreCase);

            return idx >= 0
                ? text.Substring(idx).Trim()
                : text.Trim();
        }
    }
}
