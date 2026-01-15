using System.Net;
using System.Text.Json;

namespace TelegramBotCarInsurance.Infrastructure.ExternalServices
{
    /// <summary>
    /// Base helper class for Mindee document parsers.
    /// 
    /// This class encapsulates the common Mindee V2 workflow:
    /// 1) Enqueue an inference job (upload image + model_id)
    /// 2) Poll the job status until "result_url" is available
    /// 3) Fetch the final inference JSON from "result_url"
    /// 
    /// It also provides helper methods for safe JSON navigation and field value extraction.
    /// </summary>
    public abstract class MindeeBaseParser
    {
        /// <summary>
        /// Runs Mindee V2 inference for a given model and image stream and returns the raw result JSON.
        /// 
        /// Mindee flow:
        /// - POST /v2/inferences/enqueue
        /// - Poll "job.polling_url" until "job.result_url" appears
        /// - GET "job.result_url" to obtain the final inference JSON
        /// </summary>
        /// <param name="http">HttpClient used for Mindee API calls.</param>
        /// <param name="apiKey">Mindee API key (Mindee expects it in Authorization header without Bearer).</param>
        /// <param name="modelId">Mindee model ID (custom extraction model identifier).</param>
        /// <param name="imageStream">Image stream of the document.</param>
        /// <param name="initialDelayMs">Optional delay before first polling attempt (Mindee often needs a few seconds).</param>
        /// <param name="pollingDelayMs">Delay between polling attempts.</param>
        /// <param name="maxPollAttempts">Maximum number of polling attempts before timing out.</param>
        /// <returns>Final Mindee inference JSON as a string.</returns>
        protected static async Task<string> RunInferenceAsync(
            HttpClient http,
            string apiKey,
            string modelId,
            Stream imageStream,
            int initialDelayMs,
            int pollingDelayMs,
            int maxPollAttempts)
        {
            // Validate required credentials and model
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("Mindee apiKey is empty.", nameof(apiKey));

            if (string.IsNullOrWhiteSpace(modelId))
                throw new ArgumentException("Mindee modelId is empty.", nameof(modelId));

            // Ensure correct base URL for Mindee v2
            // (Allows parsers to work even if HttpClient was not preconfigured)
            if (http.BaseAddress == null)
                http.BaseAddress = new Uri("https://api-v2.mindee.net/");

            // =========================
            // 1) ENQUEUE INFERENCE JOB
            // =========================

            // Mindee expects multipart/form-data:
            // - model_id: string
            // - file: image file content
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(modelId), "model_id");

            // Attach image stream as a file field
            using var fileContent = new StreamContent(imageStream);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            form.Add(fileContent, "file", "document.jpg");

            // Prepare enqueue request
            using var enqueueReq = new HttpRequestMessage(HttpMethod.Post, "v2/inferences/enqueue")
            {
                Content = form
            };

            // Mindee expects: Authorization: <API_KEY> (without "Bearer")
            enqueueReq.Headers.TryAddWithoutValidation("Authorization", apiKey);

            // Send enqueue request
            using var enqueueResp = await http.SendAsync(enqueueReq);
            var enqueueBody = await enqueueResp.Content.ReadAsStringAsync();

            // Fail fast if enqueue failed
            if (!enqueueResp.IsSuccessStatusCode)
                throw new InvalidOperationException($"Mindee enqueue error {(int)enqueueResp.StatusCode}: {enqueueBody}");

            // Extract job.polling_url from response
            var pollingUrl = ReadJobUrl(enqueueBody, "polling_url");
            if (string.IsNullOrWhiteSpace(pollingUrl))
                throw new InvalidOperationException($"Mindee enqueue response missing job.polling_url: {enqueueBody}");

            // Mindee recommends waiting a few seconds before polling
            if (initialDelayMs > 0)
                await Task.Delay(initialDelayMs);

            // =========================
            // 2) POLL UNTIL result_url
            // =========================

            string? resultUrl = null;

            for (int i = 0; i < maxPollAttempts; i++)
            {
                // Poll job status using the returned polling_url
                using var pollReq = new HttpRequestMessage(HttpMethod.Get, pollingUrl);
                pollReq.Headers.TryAddWithoutValidation("Authorization", apiKey);

                using var pollResp = await http.SendAsync(pollReq);
                var pollBody = await pollResp.Content.ReadAsStringAsync();

                // Mindee may return:
                // - 200 OK
                // - 202 Accepted (still processing)
                // - 302 Found (sometimes with JSON body containing job.result_url)
                if (pollResp.StatusCode == HttpStatusCode.OK ||
                    pollResp.StatusCode == HttpStatusCode.Accepted ||
                    pollResp.StatusCode == HttpStatusCode.Found)
                {
                    // Try read job.result_url
                    resultUrl = ReadJobUrl(pollBody, "result_url");
                    if (!string.IsNullOrWhiteSpace(resultUrl))
                        break;

                    // Wait and try again
                    if (pollingDelayMs > 0)
                        await Task.Delay(pollingDelayMs);

                    continue;
                }

                // Any other status code => treat as error
                throw new InvalidOperationException($"Mindee polling error {(int)pollResp.StatusCode}: {pollBody}");
            }

            // If result_url never appeared, stop the workflow
            if (string.IsNullOrWhiteSpace(resultUrl))
                throw new TimeoutException("Mindee polling timed out: job.result_url was not returned in time.");

            // =========================
            // 3) FETCH FINAL INFERENCE RESULT
            // =========================

            using var resultReq = new HttpRequestMessage(HttpMethod.Get, resultUrl);
            resultReq.Headers.TryAddWithoutValidation("Authorization", apiKey);

            using var resultResp = await http.SendAsync(resultReq);
            var resultBody = await resultResp.Content.ReadAsStringAsync();

            if (!resultResp.IsSuccessStatusCode)
                throw new InvalidOperationException($"Mindee result error {(int)resultResp.StatusCode}: {resultBody}");

            // Return raw inference JSON (parsers will extract specific fields from it)
            return resultBody;
        }

        /// <summary>
        /// Extracts a URL string from Mindee "job" JSON object:
        /// { "job": { "polling_url": "...", "result_url": "..." } }
        /// </summary>
        /// <param name="json">Raw JSON response.</param>
        /// <param name="property">Property name inside job (e.g., "polling_url" or "result_url").</param>
        protected static string? ReadJobUrl(string json, string property)
        {
            using var doc = JsonDocument.Parse(json);

            // Ensure "job" object exists
            if (!doc.RootElement.TryGetProperty("job", out var job) || job.ValueKind != JsonValueKind.Object)
                return null;

            // Ensure requested property exists
            if (!job.TryGetProperty(property, out var val))
                return null;

            // Return string value if available
            return val.ValueKind == JsonValueKind.String ? val.GetString() : null;
        }

        /// <summary>
        /// Safely navigates a JSON path: root -> a -> b -> c ...
        /// Returns false if any segment is missing or not an object.
        /// </summary>
        /// <param name="root">Root element to start from.</param>
        /// <param name="element">Output element at the end of the path.</param>
        /// <param name="path">Path segments.</param>
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
        /// Reads a field value from Mindee custom extraction output.
        /// Mindee fields can have different shapes:
        /// - fields[key] = { "value": "..." }
        /// - fields[key] = { "values": [ { "value": "..." } ] }
        /// 
        /// This helper attempts both patterns and returns the first meaningful string.
        /// </summary>
        /// <param name="fields">JSON object containing all fields.</param>
        /// <param name="key">Field key name (as defined in Mindee model).</param>
        protected static string? ReadFieldValue(JsonElement fields, string key)
        {
            // If field does not exist, return null
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
                    // Case: item is object with "value"
                    if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("value", out var v2))
                    {
                        var s = ReadPrimitiveAsString(v2);
                        if (!string.IsNullOrWhiteSpace(s)) return s;
                    }

                    // Case: item itself can be primitive
                    var s2 = ReadPrimitiveAsString(item);
                    if (!string.IsNullOrWhiteSpace(s2)) return s2;
                }
            }

            return null;
        }

        /// <summary>
        /// Converts a primitive JSON value into a string.
        /// Useful when Mindee returns numbers/booleans as field values.
        /// </summary>
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
