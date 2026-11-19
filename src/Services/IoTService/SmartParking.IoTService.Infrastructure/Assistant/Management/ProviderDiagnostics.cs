using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.IoTService.Application.Assistant.Management;

namespace SmartParking.IoTService.Infrastructure.Assistant.Management;

public sealed class ProviderDiagnostics : IProviderDiagnostics, IDisposable
{
    public const string CorrelationIdTag = "smartpark.assistant.correlation_id";
    private readonly HttpClient client;
    private readonly ILogger<ProviderDiagnostics> logger;
    private readonly bool ownsClient;
    public ProviderDiagnostics(ILogger<ProviderDiagnostics> logger)
        : this(new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
        { Timeout = Timeout.InfiniteTimeSpan }, logger) { ownsClient = true; }

    public ProviderDiagnostics(HttpClient client, ILogger<ProviderDiagnostics>? logger = null)
    {
        this.client = client;
        this.logger = logger ?? NullLogger<ProviderDiagnostics>.Instance;
    }

    private static HttpRequestMessage Request(string provider, string secret, string? model = null)
    {
        ManagementRules.Require(provider is "gemini" or "openai" && (model is null || ManagementRules.ModelId(model)));
        var request = new HttpRequestMessage(model is null ? HttpMethod.Get : HttpMethod.Post,
            provider == "openai" ? "https://api.openai.com/v1/" + (model is null ? "models" : "responses") :
            "https://generativelanguage.googleapis.com/v1beta/models" + (model is null ? "?pageSize=1000" : $"/{model}:generateContent"));
        if (provider == "openai") request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        else request.Headers.Add("x-goog-api-key", secret);
        return request;
    }
    private async Task<JsonDocument> Send(HttpRequestMessage request, string provider, string? model, CancellationToken ct)
    {
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Assistant provider diagnostics rejected. Provider {Provider}, model {Model}, upstream HTTP {UpstreamStatusCode}, correlation {CorrelationId}",
                    provider, model, (int)response.StatusCode, Activity.Current?.GetTagItem(CorrelationIdTag));
                throw new ProviderDiagnosticsException(
                    response.StatusCode == System.Net.HttpStatusCode.TooManyRequests ? 429 : 502, "PROVIDER_REJECTED");
            }
            if (response.Content.Headers.ContentLength > 1024 * 1024) throw Invalid();
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var bytes = new MemoryStream();
            var buffer = new byte[4096];
            int count;
            while ((count = await stream.ReadAsync(buffer, ct)) > 0)
            {
                if (bytes.Length + count > 1024 * 1024) throw Invalid();
                bytes.Write(buffer, 0, count);
            }
            return JsonDocument.Parse(bytes.ToArray(), new JsonDocumentOptions { MaxDepth = 32 });
        }
        catch (HttpRequestException) { throw new ProviderDiagnosticsException(503, "PROVIDER_UNAVAILABLE"); }
        catch (IOException) { throw new ProviderDiagnosticsException(503, "PROVIDER_UNAVAILABLE"); }
        catch (JsonException) { throw Invalid(); }
    }
    private static ProviderDiagnosticsException Invalid() => new(502, "INVALID_PROVIDER_RESPONSE");
    public async Task<ProviderModel[]> DiscoverAsync(string provider, string secret, CancellationToken ct)
    {
        try
        {
            var models = new Dictionary<string, ProviderModel>(StringComparer.Ordinal);
            string? page = null;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < 20; i++)
            {
                using var request = Request(provider, secret);
                if (page is not null) request.RequestUri = new Uri(request.RequestUri!.AbsoluteUri + "&pageToken=" + Uri.EscapeDataString(page));
                using var document = await Send(request, provider, null, ct);
                var root = document.RootElement;
                foreach (var item in root.GetProperty(provider == "openai" ? "data" : "models").EnumerateArray())
                {
                    var id = item.GetProperty(provider == "openai" ? "id" : "name").GetString() ?? throw Invalid();
                    if (provider == "gemini")
                    {
                        if (!id.StartsWith("models/", StringComparison.Ordinal)) throw Invalid();
                        id = id[7..];
                        if (!item.TryGetProperty("supportedGenerationMethods", out var methods) ||
                            !methods.EnumerateArray().Any(m => m.GetString() == "generateContent")) continue;
                    }
                    if (!ManagementRules.Compatible(provider, id)) continue;
                    var label = item.TryGetProperty("displayName", out var display) ? display.GetString() : id;
                    if (label is null || label.Length > 128 || label.Any(char.IsControl)) label = id;
                    models[id] = new(id, label);
                    if (models.Count > 2000) throw Invalid();
                }
                page = provider == "gemini" && root.TryGetProperty("nextPageToken", out var next) ? next.GetString() : null;
                if (string.IsNullOrEmpty(page)) return models.Values.OrderBy(m => m.Id, StringComparer.Ordinal).ToArray();
                if (page.Length > 2048 || !seen.Add(page)) throw Invalid();
            }
            throw Invalid();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
        { throw Invalid(); }
    }
    public async Task TestAsync(string provider, string model, string secret, CancellationToken ct)
    {
        try
        {
            using var request = Request(provider, secret, model);
            request.Content = provider == "openai"
                ? JsonContent.Create(new { model, input = "Reply OK", max_output_tokens = 16, store = false, stream = false })
                : JsonContent.Create(new { contents = new[] { new { role = "user", parts = new[] { new { text = "Reply OK" } } } },
                    generationConfig = GeminiProbeConfig(model) });
            using var document = await Send(request, provider, model, ct);
            var root = document.RootElement;
            string output;
            if (provider == "openai")
            {
                if (root.GetProperty("status").GetString() != "completed") throw Invalid();
                output = string.Concat(root.GetProperty("output").EnumerateArray()
                    .Where(x => x.GetProperty("type").GetString() == "message" && x.GetProperty("role").GetString() == "assistant")
                    .SelectMany(x => x.GetProperty("content").EnumerateArray())
                    .Where(x => x.GetProperty("type").GetString() == "output_text").Select(x => x.GetProperty("text").GetString()));
            }
            else
            {
                if (root.TryGetProperty("promptFeedback", out var feedback) && feedback.TryGetProperty("blockReason", out _)) throw Invalid();
                var candidates = root.GetProperty("candidates");
                if (candidates.GetArrayLength() != 1) throw Invalid();
                var finishReason = candidates[0].GetProperty("finishReason").GetString();
                if (finishReason == "MAX_TOKENS") throw new ProviderDiagnosticsException(502, "PROVIDER_OUTPUT_LIMIT");
                if (finishReason != "STOP") throw Invalid();
                var content = candidates[0].GetProperty("content");
                if (content.GetProperty("role").GetString() != "model") throw Invalid();
                output = string.Concat(content.GetProperty("parts").EnumerateArray()
                    .Where(x => !x.TryGetProperty("thought", out var thought) || !thought.GetBoolean()).Select(x => x.GetProperty("text").GetString()));
            }
            if (string.IsNullOrWhiteSpace(output) || output.Length > 1024) throw Invalid();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
        { throw Invalid(); }
    }

    private static Dictionary<string, object> GeminiProbeConfig(string model)
    {
        // Gemini counts thinking against the same output cap as visible text.
        var config = new Dictionary<string, object> { ["maxOutputTokens"] = 512 };
        if (model.StartsWith("gemini-3", StringComparison.Ordinal))
            config["thinkingConfig"] = new { thinkingLevel = "LOW", includeThoughts = false };
        return config;
    }

    public void Dispose() { if (ownsClient) client.Dispose(); }
}
