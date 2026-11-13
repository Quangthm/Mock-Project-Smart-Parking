using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using SmartParking.IoTService.API.Authentication;
using SmartParking.IoTService.API.Http;
using SmartParking.IoTService.Application.Assistant;
using SmartParking.IoTService.Application.Assistant.Authorization;

namespace SmartParking.IoTService.Tests;

public sealed class AssistantHttpContractTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TokenId = Guid.NewGuid();

    [Fact]
    public async Task SignedDriverUsesMediatorAndTrustedAccountWithoutAdvertisingFaq()
    {
        await using var host = await Host.Create();
        var token = host.Sign(Payload());
        using var response = await host.Get("/api/assistant/capabilities", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await Json(response);
        var data = json.GetProperty("data");
        Assert.False(data.GetProperty("enabled").GetBoolean());
        Assert.Equal("0.3.0", data.GetProperty("contractVersion").GetString());
        Assert.All(data.GetProperty("features").EnumerateObject(), feature => Assert.False(feature.Value.GetBoolean()));
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal(json.GetProperty("traceId").GetString(), response.Headers.GetValues("X-Correlation-ID").Single());

        using var actorResponse = await host.Get("/test/actor", token);
        var actor = await Json(actorResponse);
        Assert.Equal(AccountId, actor.GetProperty("accountId").GetGuid());
        Assert.Equal(UserId, actor.GetProperty("userId").GetGuid());
        Assert.Equal(TokenId, actor.GetProperty("accessTokenId").GetGuid());
        // No UserService lookup/store is registered: option B does not detect live revocation.
        using var repeat = await host.Get("/api/assistant/capabilities", token);
        Assert.Equal(HttpStatusCode.OK, repeat.StatusCode);
        host.Clock.UtcNow = Now.AddSeconds(900);
        using var expired = await host.Get("/api/assistant/capabilities", token);
        await AssertError(expired, 401, "INVALID_TOKEN");
    }

    [Theory]
    [InlineData("missing-sub")]
    [InlineData("missing-account_id")]
    [InlineData("missing-jti")]
    [InlineData("missing-role")]
    [InlineData("missing-exp")]
    [InlineData("empty-guid")]
    [InlineData("invalid-guid")]
    [InlineData("numeric-sub")]
    [InlineData("array-role")]
    [InlineData("duplicate-role")]
    [InlineData("duplicate-sub")]
    [InlineData("duplicate-account_id")]
    [InlineData("duplicate-jti")]
    [InlineData("blank-role")]
    [InlineData("expired")]
    [InlineData("future-nbf")]
    [InlineData("wrong-issuer")]
    [InlineData("wrong-audience")]
    [InlineData("audience-slash")]
    public async Task RejectsInvalidSignedClaims(string scenario)
    {
        await using var host = await Host.Create();
        var payload = Payload();
        if (scenario.StartsWith("missing-")) payload.Remove(scenario[8..]);
        switch (scenario)
        {
            case "empty-guid": payload["account_id"] = Guid.Empty.ToString(); break;
            case "invalid-guid": payload["jti"] = "invalid"; break;
            case "numeric-sub": payload["sub"] = 42; break;
            case "array-role": payload["role"] = new[] { "driver", "driver" }; break;
            case "blank-role": payload["role"] = " "; break;
            case "expired": payload["exp"] = Now.ToUnixTimeSeconds(); break;
            case "future-nbf": payload["nbf"] = Now.AddSeconds(1).ToUnixTimeSeconds(); break;
            case "wrong-issuer": payload["iss"] = "untrusted"; break;
            case "wrong-audience": payload["aud"] = "untrusted"; break;
            case "audience-slash": payload["aud"] = "assistant-tests/"; break;
        }
        var raw = JsonSerializer.Serialize(payload);
        if (scenario.StartsWith("duplicate-"))
        {
            var name = scenario[10..];
            raw = raw[..^1] + "," + JsonSerializer.Serialize(name) + ":" + JsonSerializer.Serialize(payload[name]) + "}";
        }
        using var response = await host.Get("/api/assistant/capabilities", host.SignRaw(raw));
        await AssertError(response, 401, "INVALID_TOKEN");
    }

    [Theory]
    [InlineData("wrong-key")]
    [InlineData("RS512")]
    [InlineData("HS256")]
    [InlineData("none")]
    [InlineData("malformed")]
    [InlineData("missing")]
    public async Task RejectsUntrustedCredentials(string scenario)
    {
        await using var host = await Host.Create();
        using var otherKey = RSA.Create(2048);
        var token = scenario switch
        {
            "wrong-key" => host.SignRaw(JsonSerializer.Serialize(Payload()), signingKey: otherKey),
            "malformed" => "not.a.jwt",
            "missing" => null,
            _ => host.SignRaw(JsonSerializer.Serialize(Payload()), scenario)
        };
        using var response = await host.Get("/api/assistant/capabilities", token);
        await AssertError(response, 401, "INVALID_TOKEN");
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("owner")]
    [InlineData("operator")]
    [InlineData("Driver")]
    public async Task OtherRolesAreForbidden(string role)
    {
        await using var host = await Host.Create();
        var payload = Payload(); payload["role"] = role;
        using var response = await host.Get("/api/assistant/capabilities", host.Sign(payload));
        await AssertError(response, 403, "ASSISTANT_FORBIDDEN");
    }

    [Theory]
    [InlineData("/absent", "GET", 404, "RESOURCE_NOT_FOUND")]
    [InlineData("/api/assistant/capabilities", "POST", 405, "METHOD_NOT_ALLOWED")]
    [InlineData("/api/assistant/capabilities?account_id=spoof", "GET", 400, "VALIDATION_FAILED")]
    [InlineData("/test/failure", "GET", 500, "INTERNAL_ERROR")]
    [InlineData("/test/large", "GET", 413, "PAYLOAD_TOO_LARGE")]
    [InlineData("/test/rate", "GET", 429, "RATE_LIMITED")]
    [InlineData("/test/binding", "POST", 415, "UNSUPPORTED_MEDIA_TYPE")]
    public async Task HttpErrorsHaveUniformEnvelope(string path, string method, int status, string code)
    {
        await using var host = await Host.Create();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Authorization = new("Bearer", host.Sign(Payload()));
        if (path == "/test/binding") request.Content = new StringContent("hello", Encoding.UTF8, "text/plain");
        using var response = await host.Client.SendAsync(request);
        await AssertError(response, status, code);
        if (status == 405) Assert.Contains("GET", response.Content.Headers.Allow);
        if (status == 429) Assert.Equal(TimeSpan.FromSeconds(2), response.Headers.RetryAfter?.Delta);
    }

    [Fact]
    public async Task BindingErrorsAreSafeAndUnknownJsonIsIgnored()
    {
        await using var host = await Host.Create();
        using var bad = await host.Client.PostAsync("/test/binding", new StringContent("{\"count\":\"secret-value\"}", Encoding.UTF8, "application/json"));
        await AssertError(bad, 400, "VALIDATION_FAILED");
        using var valid = await host.Client.PostAsync("/test/binding", new StringContent("{\"count\":1,\"accountId\":\"spoof\"}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.NoContent, valid.StatusCode);
        Assert.Empty(await valid.Content.ReadAsByteArrayAsync());
        using var head = await host.Client.SendAsync(new(HttpMethod.Head, "/absent"));
        Assert.Equal(HttpStatusCode.NotFound, head.StatusCode);
        Assert.Empty(await head.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task UnexpectedErrorsLogOnceWithoutSensitiveDetailsAndStartedResponsesPropagate()
    {
        await using var host = await Host.Create();
        using var failed = await host.Get("/test/failure", host.Sign(Payload()));
        await AssertError(failed, 500, "INTERNAL_ERROR");
        Assert.Single(host.Logs.Messages, message => message.Contains("Assistant request failed."));
        Assert.DoesNotContain(host.Logs.Messages, message => message.Contains("secret-value"));
        using var started = await host.Client.SendAsync(new(HttpMethod.Get, "/test/started"), HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        await Assert.ThrowsAnyAsync<Exception>(() => started.Content.ReadAsStringAsync());
        Assert.Single(host.Logs.Messages, message => message.Contains("Assistant request failed."));
        Assert.DoesNotContain(host.Logs.Messages, message => message.Contains("secret-value"));
    }

    [Fact]
    public void KeyMaterialMustBePublicRsaAtLeast2048Bits()
    {
        using var rsa = RSA.Create(2048);
        using var weak = RSA.Create(1024);
        using var ec = ECDsa.Create();
        foreach (var pem in new[] { "", "invalid", rsa.ExportRSAPrivateKeyPem(), rsa.ExportPkcs8PrivateKeyPem(),
            weak.ExportSubjectPublicKeyInfoPem(), ec.ExportSubjectPublicKeyInfoPem(),
            rsa.ExportSubjectPublicKeyInfoPem() + rsa.ExportSubjectPublicKeyInfoPem() })
            Assert.ThrowsAny<ArgumentException>(() => new ServiceCollection().AddAssistantAuthentication("issuer", "audience", pem, TimeProvider.System));
        new ServiceCollection().AddAssistantAuthentication("issuer", "audience", rsa.ExportRSAPublicKeyPem(), TimeProvider.System);
    }

    private static Dictionary<string, object> Payload() => new()
    {
        ["iss"] = "user-tests", ["aud"] = "assistant-tests", ["sub"] = UserId.ToString(),
        ["account_id"] = AccountId.ToString(), ["jti"] = TokenId.ToString("N"), ["role"] = "driver",
        ["exp"] = Now.AddSeconds(900).ToUnixTimeSeconds(), ["nbf"] = Now.ToUnixTimeSeconds()
    };

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    private static async Task AssertError(HttpResponseMessage response, int status, string code)
    {
        Assert.Equal(status, (int)response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("secret-value", text);
        var json = JsonDocument.Parse(text).RootElement;
        Assert.False(json.GetProperty("success").GetBoolean());
        Assert.Equal(code, json.GetProperty("code").GetString());
        Assert.Empty(json.GetProperty("errors").EnumerateArray());
        Assert.False(json.TryGetProperty("status", out _));
        Assert.False(json.TryGetProperty("statusCode", out _));
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal(json.GetProperty("traceId").GetString(), response.Headers.GetValues("X-Correlation-ID").Single());
        if (status == 401) Assert.Equal("Bearer", response.Headers.WwwAuthenticate.Single().Scheme);
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = Now;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class CapturedLogs : ILoggerProvider
    {
        public System.Collections.Concurrent.ConcurrentBag<string> Messages { get; } = [];
        public ILogger CreateLogger(string categoryName) => new Capture(this);
        public void Dispose() { }
        private sealed class Capture(CapturedLogs owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                owner.Messages.Add(formatter(state, exception) + exception?.ToString());
        }
    }

    private sealed class Host(WebApplication app, RSA key, HttpClient client, Clock clock, CapturedLogs logs) : IAsyncDisposable
    {
        public HttpClient Client => client;
        public Clock Clock => clock;
        public CapturedLogs Logs => logs;
        public static async Task<Host> Create()
        {
            var key = RSA.Create(2048);
            var clock = new Clock();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            var logs = new CapturedLogs();
            builder.Logging.ClearProviders().AddProvider(logs);
            builder.WebHost.UseTestServer();
            builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
            builder.Services.AddAssistantHttp();
            builder.Services.AddAssistantApplication();
            builder.Services.AddAssistantAuthentication("user-tests", "assistant-tests", key.ExportSubjectPublicKeyInfoPem(), clock);
            builder.Services.AddControllers().AddApplicationPart(typeof(AssistantBindingProbeController).Assembly);
            var app = builder.Build();
            app.UseAssistantHttp();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            app.MapGet("/test/actor", (ICurrentActor actor) => actor.Actor).RequireAuthorization("AssistantDriver");
            app.MapGet("/test/failure", (Func<string>)(() => throw new InvalidOperationException("secret-value")));
            app.MapGet("/test/started", async (HttpContext context) =>
            {
                await context.Response.WriteAsync("partial");
                await context.Response.Body.FlushAsync();
                throw new InvalidOperationException("secret-value");
            });
            app.MapGet("/test/large", (Func<string>)(() => throw new BadHttpRequestException("secret-value", 413)));
            app.MapGet("/test/rate", (HttpContext context) => { context.Response.Headers.RetryAfter = "2"; return Results.StatusCode(429); });
            await app.StartAsync();
            return new(app, key, app.GetTestClient(), clock, logs);
        }
        public Task<HttpResponseMessage> Get(string path, string? token)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("X-Correlation-ID", "untrusted-correlation");
            return client.SendAsync(request);
        }
        public string Sign(Dictionary<string, object> payload) => SignRaw(JsonSerializer.Serialize(payload));
        public string SignRaw(string payload, string algorithm = "RS256", RSA? signingKey = null)
        {
            var header = Base64UrlEncoder.Encode(JsonSerializer.Serialize(new { alg = algorithm, typ = "JWT" }));
            var unsigned = header + "." + Base64UrlEncoder.Encode(payload);
            var bytes = Encoding.ASCII.GetBytes(unsigned);
            var signature = algorithm switch
            {
                "none" => [],
                "HS256" => HMACSHA256.HashData(new byte[32], bytes),
                _ => (signingKey ?? key).SignData(bytes, algorithm == "RS512" ? HashAlgorithmName.SHA512 : HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            };
            return unsigned + "." + Base64UrlEncoder.Encode(signature);
        }
        public async ValueTask DisposeAsync() { client.Dispose(); await app.DisposeAsync(); key.Dispose(); }
    }
}

[ApiController]
[Route("test/binding")]
public sealed class AssistantBindingProbeController : ControllerBase
{
    [HttpPost]
    public IActionResult Bind([FromBody] BindingProbe input) => NoContent();
}
public sealed record BindingProbe(int Count);
