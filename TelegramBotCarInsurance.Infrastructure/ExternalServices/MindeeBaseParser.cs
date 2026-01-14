using System.Net;
using System.Text.Json;

namespace TelegramBotCarInsurance.Infrastructure.ExternalServices
{
    public abstract class MindeeBaseParser
    {
        protected static async Task<string> RunInferenceAsync(
            HttpClient http,
            string apiKey,
            string modelId,
            Stream imageStream,
            int initialDelayMs,
            int pollingDelayMs,
            int maxPollAttempts)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("Mindee apiKey is empty.", nameof(apiKey));

            if (string.IsNullOrWhiteSpace(modelId))
                throw new ArgumentException("Mindee modelId is empty.", nameof(modelId));

            // Ensure correct base URL for Mindee v2
            if (http.BaseAddress == null)
                http.BaseAddress = new Uri("https://api-v2.mindee.net/");

            // 1) ENQUEUE
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(modelId), "model_id");

            using var fileContent = new StreamContent(imageStream);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            form.Add(fileContent, "file", "document.jpg");

            using var enqueueReq = new HttpRequestMessage(HttpMethod.Post, "v2/inferences/enqueue")
            {
                Content = form
            };

            // Mindee expects: Authorization: <API_KEY> (no Bearer)
            enqueueReq.Headers.TryAddWithoutValidation("Authorization", apiKey);

            using var enqueueResp = await http.SendAsync(enqueueReq);
            var enqueueBody = await enqueueResp.Content.ReadAsStringAsync();

            if (!enqueueResp.IsSuccessStatusCode)
                throw new InvalidOperationException($"Mindee enqueue error {(int)enqueueResp.StatusCode}: {enqueueBody}");

            var pollingUrl = ReadJobUrl(enqueueBody, "polling_url");
            if (string.IsNullOrWhiteSpace(pollingUrl))
                throw new InvalidOperationException($"Mindee enqueue response missing job.polling_url: {enqueueBody}");

            // Mindee recommends waiting a few seconds before polling (commonly ~3s)
            if (initialDelayMs > 0)
                await Task.Delay(initialDelayMs);

            // 2) POLL UNTIL result_url
            string? resultUrl = null;

            for (int i = 0; i < maxPollAttempts; i++)
            {
                using var pollReq = new HttpRequestMessage(HttpMethod.Get, pollingUrl);
                pollReq.Headers.TryAddWithoutValidation("Authorization", apiKey);

                using var pollResp = await http.SendAsync(pollReq);
                var pollBody = await pollResp.Content.ReadAsStringAsync();

                // Mindee sometimes returns 302 but still includes JSON with job.result_url
                if (pollResp.StatusCode == HttpStatusCode.OK ||
                    pollResp.StatusCode == HttpStatusCode.Accepted ||
                    pollResp.StatusCode == HttpStatusCode.Found)
                {
                    resultUrl = ReadJobUrl(pollBody, "result_url");
                    if (!string.IsNullOrWhiteSpace(resultUrl))
                        break;

                    if (pollingDelayMs > 0)
                        await Task.Delay(pollingDelayMs);

                    continue;
                }

                throw new InvalidOperationException($"Mindee polling error {(int)pollResp.StatusCode}: {pollBody}");
            }

            if (string.IsNullOrWhiteSpace(resultUrl))
                throw new TimeoutException("Mindee polling timed out: job.result_url was not returned in time.");

            // 3) FETCH RESULT JSON
            using var resultReq = new HttpRequestMessage(HttpMethod.Get, resultUrl);
            resultReq.Headers.TryAddWithoutValidation("Authorization", apiKey);

            using var resultResp = await http.SendAsync(resultReq);
            var resultBody = await resultResp.Content.ReadAsStringAsync();

            if (!resultResp.IsSuccessStatusCode)
                throw new InvalidOperationException($"Mindee result error {(int)resultResp.StatusCode}: {resultBody}");

            return resultBody;
        }

        /// <summary>
        /// Extract URL string from Mindee job JSON: { "job": { "...": "..." } }
        /// </summary>
        protected static string? ReadJobUrl(string json, string property)
        {
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("job", out var job) || job.ValueKind != JsonValueKind.Object)
                return null;

            if (!job.TryGetProperty(property, out var val))
                return null;

            return val.ValueKind == JsonValueKind.String ? val.GetString() : null;
        }

        /// <summary>
        /// Safely navigates JSON path: root -> a -> b -> c ...
        /// </summary>
        protected static bool TryGet(JsonElement root, out JsonElement element, params string[] path)
        {
            element = root;

            foreach (var p in path)
            {
                if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(p, out element))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Reads Mindee custom extraction field value.
        /// Typical:
        /// fields[key] = { "value": "..." }
        /// Sometimes:
        /// fields[key] = { "values": [ { "value": "..." } ] }
        /// </summary>
        protected static string? ReadFieldValue(JsonElement fields, string key)
        {
            if (!fields.TryGetProperty(key, out var field))
                return null;

            // Most common: { "value": "..." }
            if (field.ValueKind == JsonValueKind.Object && field.TryGetProperty("value", out var value))
                return ReadPrimitiveAsString(value);

            // Fallback: { "values": [ { "value": "..." } ] }
            if (field.ValueKind == JsonValueKind.Object &&
                field.TryGetProperty("values", out var values) &&
                values.ValueKind == JsonValueKind.Array &&
                values.GetArrayLength() > 0)
            {
                foreach (var item in values.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("value", out var v2))
                    {
                        var s = ReadPrimitiveAsString(v2);
                        if (!string.IsNullOrWhiteSpace(s)) return s;
                    }

                    var s2 = ReadPrimitiveAsString(item);
                    if (!string.IsNullOrWhiteSpace(s2)) return s2;
                }
            }

            return null;
        }

        private static string? ReadPrimitiveAsString(JsonElement el)
        {
            return el.ValueKind switch
            {
                JsonValueKind.String => el.GetString(),
                JsonValueKind.Number => el.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null
            };
        }
    }
}
