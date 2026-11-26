using System.Net;
using System.Text;
using System.Text.Json;
using SmartParking.IoTService.Application.Assistant.Management;
using SmartParking.IoTService.Infrastructure.Assistant.Management;

namespace SmartParking.IoTService.Tests;

public sealed class ProviderDiagnosticsTests
{
    [Fact]
    public async Task GeminiDiscoveryFollowsPagesFiltersAndSortsWithoutSecretInUrl()
    {
        var calls = new List<string>();
        using var http = new HttpClient(new FakeHttp(async (request, ct) =>
        {
            calls.Add(request.RequestUri!.AbsoluteUri);
            Assert.DoesNotContain("synthetic-secret", request.RequestUri.AbsoluteUri);
            Assert.Equal("synthetic-secret", request.Headers.GetValues("x-goog-api-key").Single());
            await Task.Yield();
            return Json(calls.Count == 1
                ? """{"models":[{"name":"models/gemini-3.8-flash","supportedGenerationMethods":["generateContent"]},{"name":"models/gemini-embed","supportedGenerationMethods":["embedContent"]}],"nextPageToken":"a+b"}"""
                : """{"models":[{"name":"models/gemini-3.6-flash","displayName":"A","supportedGenerationMethods":["generateContent"]}]}""");
        }));
        using var provider = new ProviderDiagnostics(http);
        var result = await provider.DiscoverAsync("gemini", "synthetic-secret", default);
        Assert.Equal(["gemini-3.6-flash", "gemini-3.8-flash"], result.Select(x => x.Id));
        Assert.Contains("pageToken=a%2Bb", calls[1]);
    }

    [Fact]
    public async Task OpenAiDiscoveryDoesNotTreatEmbeddingOrUnknownModelAsChatCompatible()
    {
        using var http = new HttpClient(new FakeHttp((request, _) =>
        {
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            return Task.FromResult(Json("""{"data":[{"id":"text-embedding-3-small"},{"id":"gpt-4.1"},{"id":"unknown-chat-model"}]}"""));
        }));
        using var provider = new ProviderDiagnostics(http);
        Assert.Equal("gpt-4.1", Assert.Single(await provider.DiscoverAsync("openai", "synthetic-secret", default)).Id);
    }

    [Theory]
    [InlineData("openai", "gpt-4.1", "{\"status\":\"completed\",\"output\":[{\"type\":\"message\",\"role\":\"assistant\",\"content\":[{\"type\":\"output_text\",\"text\":\"OK\"}]}]}")]
    [InlineData("gemini", "gemini-3.6-flash", "{\"candidates\":[{\"finishReason\":\"STOP\",\"content\":{\"role\":\"model\",\"parts\":[{\"text\":\"OK\"}]}}]}")]
    public async Task SyntheticTestUsesExactModelAndBoundedBudget(string name, string model, string response)
    {
        var calls = 0;
        using var http = new HttpClient(new FakeHttp(async (request, ct) =>
        {
            calls++;
            var body = await request.Content!.ReadAsStringAsync(ct);
            Assert.Contains("Reply OK", body);
            Assert.DoesNotContain("knowledge", body);
            using var doc = JsonDocument.Parse(body);
            if (name == "openai")
            {
                Assert.Equal(model, doc.RootElement.GetProperty("model").GetString());
                Assert.Equal(16, doc.RootElement.GetProperty("max_output_tokens").GetInt32());
                Assert.False(doc.RootElement.GetProperty("store").GetBoolean());
            }
            else
            {
                Assert.EndsWith($"/{model}:generateContent", request.RequestUri!.AbsoluteUri);
                var config = doc.RootElement.GetProperty("generationConfig");
                Assert.Equal(512, config.GetProperty("maxOutputTokens").GetInt32());
                Assert.Equal("LOW", config.GetProperty("thinkingConfig").GetProperty("thinkingLevel").GetString());
            }
            return Json(response);
        }));
        using var provider = new ProviderDiagnostics(http);
        await provider.TestAsync(name, model, "synthetic-secret", default);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task GeminiTokenExhaustionIsReportedWithoutPassing()
    {
        using var http = new HttpClient(new FakeHttp((_, _) => Task.FromResult(Json(
            """{"candidates":[{"finishReason":"MAX_TOKENS"}]}"""))));
        using var provider = new ProviderDiagnostics(http);
        var error = await Assert.ThrowsAsync<ProviderDiagnosticsException>(() =>
            provider.TestAsync("gemini", "gemini-3.6-flash", "synthetic-secret", default));
        Assert.Equal("PROVIDER_OUTPUT_LIMIT", error.Code);
    }

    [Theory]
    [InlineData(401, 502)]
    [InlineData(429, 429)]
    [InlineData(503, 502)]
    [InlineData(302, 502)]
    public async Task RejectionsAreSanitizedAndNeverRetried(int upstream, int expected)
    {
        var calls = 0;
        using var http = new HttpClient(new FakeHttp((_, _) =>
        { calls++; return Task.FromResult(new HttpResponseMessage((HttpStatusCode)upstream) { Content = new StringContent("secret upstream body") }); }));
        using var provider = new ProviderDiagnostics(http);
        var error = await Assert.ThrowsAsync<ProviderDiagnosticsException>(() => provider.TestAsync("openai", "gpt-4.1", "synthetic-secret", default));
        Assert.Equal(expected, error.Status);
        Assert.DoesNotContain("secret upstream body", error.ToString());
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"status\":\"incomplete\",\"output\":[]}")]
    [InlineData("{\"status\":\"completed\",\"output\":[]}")]
    [InlineData("not-json")]
    public async Task InvalidGenerationNeverPasses(string body)
    {
        using var http = new HttpClient(new FakeHttp((_, _) => Task.FromResult(Json(body))));
        using var provider = new ProviderDiagnostics(http);
        await Assert.ThrowsAsync<ProviderDiagnosticsException>(() => provider.TestAsync("openai", "gpt-4.1", "synthetic-secret", default));
    }

    [Fact]
    public async Task NetworkFailureAndCancellationAreNotSuccessfulGeneration()
    {
        using var http = new HttpClient(new FakeHttp((_, _) => throw new HttpRequestException("sensitive URL")));
        using var provider = new ProviderDiagnostics(http);
        var error = await Assert.ThrowsAsync<ProviderDiagnosticsException>(() => provider.TestAsync("openai", "gpt-4.1", "synthetic-secret", default));
        Assert.Equal(503, error.Status);
        Assert.DoesNotContain("sensitive", error.ToString());
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        using var http2 = new HttpClient(new FakeHttp((_, ct) => Task.FromCanceled<HttpResponseMessage>(ct)));
        using var provider2 = new ProviderDiagnostics(http2);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider2.TestAsync("openai", "gpt-4.1", "synthetic-secret", cancelled.Token));
    }

    [Fact]
    public void CursorBindsCredentialAndExpiresWithoutCachingUnsavedCredential()
    {
        var clock = new Clock(); var cache = new ModelDiscoveryCache(clock);
        var binding = cache.Bind("actor:provider:synthetic-secret");
        var first = cache.Page(binding, null, Enumerable.Range(0, 101).Select(i => new ProviderModel($"model-{i:D3}", "Model")).ToArray(), false);
        Assert.Equal(100, first.Items.Length);
        Assert.Null(cache.Get(binding));
        Assert.Throws<ProviderDiagnosticsException>(() => cache.Page(cache.Bind("changed-secret"), first.NextCursor, null, false));
        Assert.Single(cache.Page(binding, first.NextCursor, null, false).Items);
        clock.Now = clock.Now.AddMinutes(5);
        Assert.Throws<ProviderDiagnosticsException>(() => cache.Page(binding, first.NextCursor, null, false));
    }
    [Fact]
    public void ReadingSavedPagesDoesNotExtendTheirLifetime()
    {
        var clock = new Clock();
        var cache = new ModelDiscoveryCache(clock);
        var first = cache.Page("saved", null, [new("model", "Model")], true);
        clock.Now = clock.Now.AddMinutes(4);
        Assert.Equal(first.AsOf, cache.Get("saved")!.AsOf);
        clock.Now = clock.Now.AddMinutes(1);
        Assert.Null(cache.Get("saved"));
    }

    private sealed class Clock : TimeProvider
    { public DateTimeOffset Now = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Now; }
    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    private sealed class FakeHttp(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken); }
}
