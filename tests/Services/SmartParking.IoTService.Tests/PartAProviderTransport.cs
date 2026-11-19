using System.Net;
using System.Text.Json;

namespace SmartParking.IoTService.Tests;

internal sealed class PartAProviderTransport(string? failure = null) : HttpMessageHandler, IHttpClientFactory
{
    internal List<JsonElement> Inputs { get; } = [];
    public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (failure == "timeout") await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        if (failure == "unavailable") return new(HttpStatusCode.ServiceUnavailable);
        using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
        var openAi = request.RequestUri!.Host == "api.openai.com";
        var input = openAi ? body.RootElement.GetProperty("input").GetString()!
            : body.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString()!;
        using var parsed = JsonDocument.Parse(input);
        Inputs.Add(parsed.RootElement.Clone());
        var text = JsonSerializer.Serialize(new {
            reply = parsed.RootElement.GetProperty("answerLanguage").GetString() == "en"
                ? "Synthetic English answer from the supplied source."
                : "Câu trả lời tiếng Việt giả lập từ nguồn đã gửi.",
            sourceIds = new[] { parsed.RootElement.GetProperty("sources")[0].GetProperty("id").GetString() }
        });
        var response = openAi
            ? JsonSerializer.Serialize(new { status = "completed", output = new[] {
                new { type = "message", role = "assistant", content = new[] { new { type = "output_text", text } } } } })
            : JsonSerializer.Serialize(new { candidates = new[] {
                new { finishReason = "STOP", content = new { role = "model", parts = new[] { new { text } } } } } });
        return new(HttpStatusCode.OK) { Content = new StringContent(response) };
    }
}
