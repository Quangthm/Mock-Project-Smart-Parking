using System.Net;
using System.Text;
using System.Text.Json;

namespace SmartParking.IoTService.Tests;

internal sealed class PublicKnowledgeHttp : HttpMessageHandler
{
    public string Body = ""; public string Provider = ""; public string? Key;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Body = await request.Content!.ReadAsStringAsync(ct);
        Key = request.Headers.TryGetValues("x-goog-api-key", out var keys) ? keys.Single() : null;
        var answer = JsonSerializer.Serialize(new { reply = "Synthetic answer.", sourceIds = new[] { "public-topic" } });
        object envelope = Provider == "openai"
            ? new { status = "completed", output = new[] { new { type = "message", role = "assistant", content = new[] { new { type = "output_text", text = answer } } } } }
            : new { candidates = new[] { new { finishReason = "STOP", content = new { role = "model", parts = new[] { new { text = answer } } } } } };
        return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(envelope), Encoding.UTF8, "application/json") };
    }
}
