using System.Net;
using System.Text.Json;
using System.Text;
using SmartParking.IoTService.Application.Assistant.Knowledge;
using SmartParking.IoTService.Infrastructure.Assistant.Knowledge;

namespace SmartParking.IoTService.Tests;

public sealed class DemoGroundedGeneratorTests
{
    [Theory]
    [InlineData("openai")]
    [InlineData("gemini")]
    public async Task UsesOnlyContentAndValidatesSources(string provider)
    {
        using var corpus = new Corpus();
        var handler = new FakeHandler(provider, "booking-guide");
        using var client = new HttpClient(handler);
        using var generator = new DemoGroundedGenerator(new(corpus.Path, provider, "model-test", "synthetic-key"), client);
        var answer = await generator.GenerateAsync("booking instructions", default);
        Assert.Single(answer.Sources);
        Assert.Equal("booking-guide", answer.Sources[0].ArticleId);
        Assert.DoesNotContain("PROVENANCE_SECRET", handler.RequestBody);
        Assert.DoesNotContain("draft", handler.RequestBody);
        Assert.Equal(provider == "openai" ? "api.openai.com" : "generativelanguage.googleapis.com", handler.Uri!.Host);
        Assert.DoesNotContain("synthetic-key", handler.Uri.ToString());
        Assert.Equal(1, handler.Calls);
        using var sent = JsonDocument.Parse(handler.RequestBody);
        var inputText = provider == "openai" ? sent.RootElement.GetProperty("input").GetString()!
            : sent.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString()!;
        using var input = JsonDocument.Parse(inputText);
        // This is the text the model sees AFTER the outer HTTP JSON has been decoded.
        Assert.Contains("Đặt chỗ", inputText);
        Assert.DoesNotContain("\\u0110", inputText, StringComparison.OrdinalIgnoreCase);
        Assert.True(Encoding.UTF8.GetByteCount(inputText) < Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(input.RootElement)));
        Assert.Equal("en", input.RootElement.GetProperty("answerLanguage").GetString());
        Assert.Equal("vi", input.RootElement.GetProperty("sourceLanguage").GetString());
        Assert.Equal("Đặt chỗ bằng form. Thời hạn giữ mặc định 5 phút.", input.RootElement.GetProperty("sources")[0].GetProperty("Content").GetString());
        if (provider == "gemini")
        {
            var config = sent.RootElement.GetProperty("generationConfig");
            Assert.False(config.TryGetProperty("candidateCount", out _));
            Assert.Equal("APPLICATION_JSON", config.GetProperty("responseFormat").GetProperty("text").GetProperty("mimeType").GetString());
            Assert.Equal("LOW", config.GetProperty("thinkingConfig").GetProperty("thinkingLevel").GetString());
        }
        else Assert.False(sent.RootElement.GetProperty("store").GetBoolean());
    }

    [Theory]
    [InlineData("invented")]
    public async Task RejectsInventedSources(string id)
    {
        using var corpus = new Corpus(); using var client = new HttpClient(new FakeHandler("openai", id));
        using var generator = new DemoGroundedGenerator(new(corpus.Path, "openai", "model-test", "synthetic-key"), client);
        await Assert.ThrowsAsync<DemoGroundedUnavailableException>(() => generator.GenerateAsync("booking", default));
    }

    [Theory]
    [InlineData("gemini", "booking", "I can help with SmartPark")]
    [InlineData("openai", "booking", "I can help with SmartPark")]
    [InlineData("gemini", "đặt chỗ", "Mình hiện hỗ trợ")]
    [InlineData("openai", "đặt chỗ", "Mình hiện hỗ trợ")]
    public async Task ExplicitNoAnswerDiscardsUncitedModelTextAndReturnsFaqBoundary(string provider, string question, string boundary)
    {
        using var corpus = new Corpus();
        var handler = new FakeHandler(provider, "") { OutputOverride = "{\"reply\":\"Your booking is confirmed and payment was charged.\",\"sourceIds\":[]}" };
        using var client = new HttpClient(handler);
        using var generator = new DemoGroundedGenerator(new(corpus.Path, provider, "model-test", "synthetic-key"), client);
        var answer = await generator.GenerateAsync(question, default);
        Assert.Empty(answer.Sources);
        Assert.StartsWith(boundary, answer.Reply);
        Assert.DoesNotContain("confirmed", answer.Reply); // Uncited model claims must never surface.
        Assert.DoesNotContain("charged", answer.Reply);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("{\"reply\":\"uncited\"}")]
    [InlineData("{\"reply\":\"uncited\",\"sourceIds\":null}")]
    [InlineData("{\"reply\":\"uncited\",\"sourceIds\":\"booking-guide\"}")]
    [InlineData("{\"reply\":\"uncited\",\"sourceIds\":[null]}")]
    [InlineData("{\"reply\":\"uncited\",\"sourceIds\":[\"booking-guide\",\"booking-guide\"]}")]
    [InlineData("{\"reply\":\"\",\"sourceIds\":[]}")]
    public async Task MalformedOrMissingCitationsAreStillTechnicalFailures(string text)
    {
        using var corpus = new Corpus();
        var handler = new FakeHandler("gemini", "booking-guide") { OutputOverride = text };
        using var client = new HttpClient(handler);
        using var generator = new DemoGroundedGenerator(new(corpus.Path, "gemini", "model-test", "synthetic-key"), client);
        var error = await Assert.ThrowsAsync<DemoGroundedUnavailableException>(() => generator.GenerateAsync("booking", default));
        Assert.Equal(DemoGroundedFailure.InvalidResponse, error.Failure);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("gemini")]
    [InlineData("openai")]
    public async Task UnicodePromptStillRoundTripsQuotesNewlinesBackslashesAndMarkupAsData(string provider)
    {
        using var corpus = new Corpus();
        var handler = new FakeHandler(provider, "booking-guide");
        using var client = new HttpClient(handler);
        using var generator = new DemoGroundedGenerator(new(corpus.Path, provider, "model-test", "synthetic-key"), client);
        const string question = "booking \"Đặt chỗ\" <script>test</script>\nđường \\ dẫn";
        await generator.GenerateAsync(question, default);
        using var body = JsonDocument.Parse(handler.RequestBody);
        var inputText = provider == "openai" ? body.RootElement.GetProperty("input").GetString()!
            : body.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString()!;
        using var input = JsonDocument.Parse(inputText);
        Assert.Equal(question, input.RootElement.GetProperty("question").GetString());
        Assert.Contains("Đặt chỗ", inputText);
        Assert.DoesNotContain("<script>", inputText);
        Assert.Equal(1, handler.Calls);
        Assert.DoesNotContain("synthetic-key", handler.RequestBody);
    }
    [Theory]
    [InlineData(302)]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task ProviderFailureHasNoFallbackOrRetry(int status)
    {
        using var corpus = new Corpus(); var handler = new FakeHandler("gemini", "booking-guide") { Status = status };
        using var client = new HttpClient(handler);
        using var generator = new DemoGroundedGenerator(new(corpus.Path, "gemini", "model-test", "synthetic-key"), client);
        var exception = await Assert.ThrowsAsync<DemoGroundedUnavailableException>(() => generator.GenerateAsync("booking", default));
        Assert.DoesNotContain("provider-sensitive", exception.ToString()); Assert.Equal(1, handler.Calls);
        Assert.Equal(DemoGroundedFailure.ProviderRejected, exception.Failure);
        Assert.Equal(status, exception.ProviderStatus);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TransportAndTimeoutFailuresAreDistinguishedWithoutRawExceptions(bool timeout)
    {
        using var corpus = new Corpus();
        using var client = new HttpClient(new FailedTransport(timeout));
        using var generator = new DemoGroundedGenerator(new(corpus.Path, "gemini", "model-test", "synthetic-key"), client);
        var exception = await Assert.ThrowsAsync<DemoGroundedUnavailableException>(() => generator.GenerateAsync("booking", default));
        Assert.Equal(timeout ? DemoGroundedFailure.Timeout : DemoGroundedFailure.Network, exception.Failure);
        Assert.Null(exception.ProviderStatus);
        Assert.DoesNotContain("sensitive-transport-message", exception.ToString());
    }
    [Fact]
    public async Task NoMatchDoesNotSpendAndCancellationPropagates()
    {
        using var corpus = new Corpus(); var handler = new FakeHandler("openai", "booking-guide");
        using var client = new HttpClient(handler);
        using var generator = new DemoGroundedGenerator(new(corpus.Path, "openai", "model-test", "synthetic-key"), client);
        Assert.Empty((await generator.GenerateAsync("astronomy", default)).Sources);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => generator.GenerateAsync("booking", new CancellationToken(true)));
        Assert.Equal(0, handler.Calls);
    }
    [Fact]
    public async Task OversizeProviderOutputIsRejected()
    {
        using var corpus = new Corpus(); var handler = new FakeHandler("openai", "booking-guide") { Oversize = true };
        using var client = new HttpClient(handler);
        using var generator = new DemoGroundedGenerator(new(corpus.Path, "openai", "model-test", "synthetic-key"), client);
        await Assert.ThrowsAsync<DemoGroundedUnavailableException>(() => generator.GenerateAsync("booking", default));
    }
    [Theory]
    [InlineData("What’s the cancellation policy?", "cancellation-refund-guide", "Hủy")]
    [InlineData("How do I pay? 💳", "payment-guide", "Thanh toán")]
    [InlineData("Tôi muốn đặt chỗ", "booking-guide", "Đặt chỗ")]
    [InlineData("toi muon dat cho", "booking-guide", "Đặt chỗ")]
    public async Task SingleVietnameseCorpusAnswersBothLanguages(string question, string id, string title)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SmartParking.slnx"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var corpus = Path.Combine(directory.FullName, "docs", "03-design", "ai", "p1", "knowledge");
        Assert.Single(Directory.GetFiles(corpus, "*.md"));
        var handler = new FakeHandler("openai", id);
        using var client = new HttpClient(handler);
        using var generator = new DemoGroundedGenerator(new(corpus, "openai", "model-test", "synthetic-key"), client);
        var answer = await generator.GenerateAsync(question, default);
        Assert.Contains(title, Assert.Single(answer.Sources).Title, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Provenance", handler.RequestBody);
    }
    private sealed class Corpus : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "smartpark-grounded-test-" + Guid.NewGuid().ToString("N"));
        public Corpus()
        {
            Directory.CreateDirectory(Path);
            File.WriteAllText(System.IO.Path.Combine(Path, "smartpark-knowledge.md"), "---\nversion: 0.2.0\nlocale: vi\npublication_state: draft\n---\n# Kiến thức SmartPark\n\n## [booking-guide] Đặt chỗ\n\nTừ khóa: booking; đặt chỗ\n\n### Nội dung\n\nĐặt chỗ bằng form. Thời hạn giữ mặc định 5 phút.\n\n### Đối chiếu — không gửi đến AI\nPROVENANCE_SECRET");
        }
        public void Dispose() { File.Delete(System.IO.Path.Combine(Path, "smartpark-knowledge.md")); Directory.Delete(Path); }
    }
    private sealed class FakeHandler(string provider, string id) : HttpMessageHandler
    {
        public int Status = 200; public int Calls; public bool Oversize;
        public string RequestBody = ""; public Uri? Uri;
        public string? OutputOverride;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++; Uri = request.RequestUri; RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            var text = OutputOverride ?? JsonSerializer.Serialize(new { reply = "Use the booking form.", sourceIds = id.Length == 0 ? Array.Empty<string>() : [id] });
            var body = provider == "openai"
                ? JsonSerializer.Serialize(new { status = "completed", output = new[] { new { type = "message", role = "assistant", content = new[] { new { type = "output_text", text } } } } })
                : JsonSerializer.Serialize(new { candidates = new[] { new { finishReason = "STOP", content = new { role = "model", parts = new[] { new { text } } } } } });
            return new((HttpStatusCode)Status) { Content = new StringContent(Oversize ? new string('x', 131073) : Status == 200 ? body : "provider-sensitive") };
        }
    }
    private sealed class FailedTransport(bool timeout) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromException<HttpResponseMessage>(timeout
                ? new TaskCanceledException("sensitive-transport-message")
                : new HttpRequestException("sensitive-transport-message"));
    }
}
