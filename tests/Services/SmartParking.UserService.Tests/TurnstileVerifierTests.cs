using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UserService.API;
using UserService.API.Common;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;
using UserService.Infrastructure.Configurations;
using UserService.Infrastructure.Services;

namespace SmartParking.UserService.Tests;

public sealed class TurnstileVerifierTests
{
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? HandlerFunc { get; set; }
        public List<HttpRequestMessage> CapturedRequests { get; } = [];
        public List<string> CapturedBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CapturedRequests.Add(request);
            if (request.Content is not null)
            {
                CapturedBodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            }

            if (HandlerFunc is not null)
            {
                return await HandlerFunc(request, cancellationToken);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"success\":true,\"action\":\"driver_register\",\"hostname\":\"localhost\"}", Encoding.UTF8, "application/json")
            };
        }
    }

    private static (TurnstileVerifier Verifier, MockHttpMessageHandler Handler, TurnstileOptions Options) CreateVerifier(
        bool enabled = true,
        string secretKey = "test-secret-key",
        string[]? expectedHostnames = null)
    {
        var options = new TurnstileOptions
        {
            Enabled = enabled,
            SecretKey = secretKey,
            ExpectedHostnames = expectedHostnames ?? ["localhost", "127.0.0.1", "smartpark.vn"]
        };
        var handler = new MockHttpMessageHandler();
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
        var verifier = new TurnstileVerifier(client, options);
        return (verifier, handler, options);
    }

    [Fact]
    public async Task VerifyAsync_WhenDisabled_CompletesImmediatelyWithoutHttpCall()
    {
        var (verifier, handler, _) = CreateVerifier(enabled: false);

        await verifier.VerifyAsync(null, "driver_register", CancellationToken.None);

        Assert.Empty(handler.CapturedRequests);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public async Task VerifyAsync_WhenTokenIsNullOrWhitespace_ThrowsBotVerificationRequired400_WithoutHttpCall(string? token)
    {
        var (verifier, handler, _) = CreateVerifier();

        var ex = await Assert.ThrowsAsync<AuthException>(() =>
            verifier.VerifyAsync(token, "driver_register", CancellationToken.None));

        Assert.Equal("BOT_VERIFICATION_REQUIRED", ex.Code);
        Assert.Equal(400, ex.StatusCode);
        Assert.Empty(handler.CapturedRequests);
    }

    [Fact]
    public async Task VerifyAsync_WhenTokenLengthExceeds2048_ThrowsBotVerificationFailed400_WithoutHttpCall()
    {
        var (verifier, handler, _) = CreateVerifier();
        var longToken = new string('x', 2049);

        var ex = await Assert.ThrowsAsync<AuthException>(() =>
            verifier.VerifyAsync(longToken, "driver_register", CancellationToken.None));

        Assert.Equal("BOT_VERIFICATION_FAILED", ex.Code);
        Assert.Equal(400, ex.StatusCode);
        Assert.Empty(handler.CapturedRequests);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task VerifyAsync_WhenEnabledAndSecretKeyMissing_ThrowsBotVerificationUnavailable503_WithoutHttpCall(string secretKey)
    {
        var (verifier, handler, _) = CreateVerifier(secretKey: secretKey);

        var ex = await Assert.ThrowsAsync<AuthException>(() =>
            verifier.VerifyAsync("valid-token", "driver_register", CancellationToken.None));

        Assert.Equal("BOT_VERIFICATION_UNAVAILABLE", ex.Code);
        Assert.Equal(503, ex.StatusCode);
        Assert.Empty(handler.CapturedRequests);
    }

    [Fact]
    public async Task VerifyAsync_ValidToken_CloudflareSuccess_MatchingActionAndHostname_Succeeds()
    {
        var (verifier, handler, _) = CreateVerifier();
        handler.HandlerFunc = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new TurnstileVerifyResponse
                {
                    Success = true,
                    Action = "driver_register",
                    Hostname = "smartpark.vn"
                }),
                Encoding.UTF8, "application/json")
        });

        await verifier.VerifyAsync("valid-token", "driver_register", CancellationToken.None);

        var request = Assert.Single(handler.CapturedRequests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(new Uri("https://challenges.cloudflare.com/turnstile/v0/siteverify"), request.RequestUri);
        var body = Assert.Single(handler.CapturedBodies);
        Assert.Contains("secret=test-secret-key", body);
        Assert.Contains("response=valid-token", body);
    }

    [Fact]
    public async Task VerifyAsync_WhenRemoteIpProvided_IncludesRemoteIpInBody()
    {
        var (verifier, handler, _) = CreateVerifier();
        handler.HandlerFunc = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"success\":true,\"action\":\"owner_register\",\"hostname\":\"localhost\"}", Encoding.UTF8, "application/json")
        });

        await verifier.VerifyAsync("valid-token", "owner_register", CancellationToken.None, remoteIp: "198.51.100.42");

        var body = Assert.Single(handler.CapturedBodies);
        Assert.Contains("remoteip=198.51.100.42", body);
    }

    [Fact]
    public async Task VerifyAsync_WhenSuccessTrueButActionMismatches_ThrowsBotVerificationFailed400()
    {
        var (verifier, handler, _) = CreateVerifier();
        handler.HandlerFunc = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new TurnstileVerifyResponse
                {
                    Success = true,
                    Action = "password_login",
                    Hostname = "localhost"
                }),
                Encoding.UTF8, "application/json")
        });

        var ex = await Assert.ThrowsAsync<AuthException>(() =>
            verifier.VerifyAsync("valid-token", "driver_register", CancellationToken.None));

        Assert.Equal("BOT_VERIFICATION_FAILED", ex.Code);
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task VerifyAsync_WhenSuccessTrueButHostnameNotInAllowlist_ThrowsBotVerificationFailed400()
    {
        var (verifier, handler, _) = CreateVerifier(expectedHostnames: ["smartpark.vn"]);
        handler.HandlerFunc = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new TurnstileVerifyResponse
                {
                    Success = true,
                    Action = "driver_register",
                    Hostname = "malicious-site.com"
                }),
                Encoding.UTF8, "application/json")
        });

        var ex = await Assert.ThrowsAsync<AuthException>(() =>
            verifier.VerifyAsync("valid-token", "driver_register", CancellationToken.None));

        Assert.Equal("BOT_VERIFICATION_FAILED", ex.Code);
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task VerifyAsync_HostnameComparisonIsCaseInsensitive()
    {
        var (verifier, handler, _) = CreateVerifier(expectedHostnames: ["SmartPark.VN"]);
        handler.HandlerFunc = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new TurnstileVerifyResponse
                {
                    Success = true,
                    Action = "driver_register",
                    Hostname = "smartpark.vn"
                }),
                Encoding.UTF8, "application/json")
        });

        await verifier.VerifyAsync("valid-token", "driver_register", CancellationToken.None);
        Assert.Single(handler.CapturedRequests);
    }

    [Fact]
    public async Task VerifyAsync_WhenSuccessFalseWithTimeoutOrDuplicate_ThrowsBotVerificationExpired400()
    {
        var (verifier, handler, _) = CreateVerifier();
        handler.HandlerFunc = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new TurnstileVerifyResponse
                {
                    Success = false,
                    ErrorCodes = ["timeout-or-duplicate"]
                }),
                Encoding.UTF8, "application/json")
        });

        var ex = await Assert.ThrowsAsync<AuthException>(() =>
            verifier.VerifyAsync("valid-token", "driver_register", CancellationToken.None));

        Assert.Equal("BOT_VERIFICATION_EXPIRED", ex.Code);
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task VerifyAsync_WhenSuccessFalseWithInvalidInputResponse_ThrowsBotVerificationFailed400()
    {
        var (verifier, handler, _) = CreateVerifier();
        handler.HandlerFunc = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new TurnstileVerifyResponse
                {
                    Success = false,
                    ErrorCodes = ["invalid-input-response"]
                }),
                Encoding.UTF8, "application/json")
        });

        var ex = await Assert.ThrowsAsync<AuthException>(() =>
            verifier.VerifyAsync("valid-token", "driver_register", CancellationToken.None));

        Assert.Equal("BOT_VERIFICATION_FAILED", ex.Code);
        Assert.Equal(400, ex.StatusCode);
    }

    [Theory]
    [InlineData("missing-input-response")]
    [InlineData("bad-request")]
    [InlineData("missing-input-secret")]
    [InlineData("invalid-input-secret")]
    [InlineData("internal-error")]
    [InlineData("unknown-provider-code")]
    public async Task VerifyAsync_WhenSuccessFalseWithProviderOrConfigError_ThrowsBotVerificationUnavailable503(string errorCode)
    {
        var (verifier, handler, _) = CreateVerifier();
        handler.HandlerFunc = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new TurnstileVerifyResponse
                {
                    Success = false,
                    ErrorCodes = [errorCode]
                }),
                Encoding.UTF8, "application/json")
        });

        var ex = await Assert.ThrowsAsync<AuthException>(() =>
            verifier.VerifyAsync("valid-token", "driver_register", CancellationToken.None));

        Assert.Equal("BOT_VERIFICATION_UNAVAILABLE", ex.Code);
        Assert.Equal(503, ex.StatusCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task VerifyAsync_WhenCloudflareReturnsNon2xx_ThrowsBotVerificationUnavailable503(HttpStatusCode statusCode)
    {
        var (verifier, handler, _) = CreateVerifier();
        handler.HandlerFunc = (_, _) => Task.FromResult(new HttpResponseMessage(statusCode));

        var ex = await Assert.ThrowsAsync<AuthException>(() =>
            verifier.VerifyAsync("valid-token", "driver_register", CancellationToken.None));

        Assert.Equal("BOT_VERIFICATION_UNAVAILABLE", ex.Code);
        Assert.Equal(503, ex.StatusCode);
    }

    [Fact]
    public async Task VerifyAsync_WhenTimeoutOccurs_ThrowsBotVerificationUnavailable503()
    {
        var (verifier, handler, _) = CreateVerifier();
        // Provider timeout causes OperationCanceledException while ct.IsCancellationRequested is false
        handler.HandlerFunc = (_, _) => throw new OperationCanceledException();

        var ex = await Assert.ThrowsAsync<AuthException>(() =>
            verifier.VerifyAsync("valid-token", "driver_register", CancellationToken.None));

        Assert.Equal("BOT_VERIFICATION_UNAVAILABLE", ex.Code);
        Assert.Equal(503, ex.StatusCode);
        Assert.Contains("timed out", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VerifyAsync_WhenClientCancelsRequest_PropagatesOperationCanceledExceptionDirectly()
    {
        var (verifier, handler, _) = CreateVerifier();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        handler.HandlerFunc = (_, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            verifier.VerifyAsync("valid-token", "driver_register", cts.Token));
    }

    [Fact]
    public async Task VerifyAsync_WhenHttpRequestException_ThrowsBotVerificationUnavailable503()
    {
        var (verifier, handler, _) = CreateVerifier();
        handler.HandlerFunc = (_, _) => throw new HttpRequestException("DNS resolution failed");

        var ex = await Assert.ThrowsAsync<AuthException>(() =>
            verifier.VerifyAsync("valid-token", "driver_register", CancellationToken.None));

        Assert.Equal("BOT_VERIFICATION_UNAVAILABLE", ex.Code);
        Assert.Equal(503, ex.StatusCode);
    }

    [Fact]
    public async Task VerifyAsync_WhenMalformedJson_ThrowsBotVerificationUnavailable503()
    {
        var (verifier, handler, _) = CreateVerifier();
        handler.HandlerFunc = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<!DOCTYPE html><html><body>Error</body></html>", Encoding.UTF8, "text/html")
        });

        var ex = await Assert.ThrowsAsync<AuthException>(() =>
            verifier.VerifyAsync("valid-token", "driver_register", CancellationToken.None));

        Assert.Equal("BOT_VERIFICATION_UNAVAILABLE", ex.Code);
        Assert.Equal(503, ex.StatusCode);
    }

    [Fact]
    public async Task VerifyAsync_WhenIOException_ThrowsBotVerificationUnavailable503()
    {
        var (verifier, handler, _) = CreateVerifier();
        handler.HandlerFunc = (_, _) => throw new IOException("Connection forcibly closed");

        var ex = await Assert.ThrowsAsync<AuthException>(() =>
            verifier.VerifyAsync("valid-token", "driver_register", CancellationToken.None));

        Assert.Equal("BOT_VERIFICATION_UNAVAILABLE", ex.Code);
        Assert.Equal(503, ex.StatusCode);
    }

    [Fact]
    public void ApiExceptionMapper_MapsAllFourBotVerificationCodesToApprovedStatusAndMessage()
    {
        var mapper = ApiExceptionMapper.CreateDefault();

        var required = mapper.Map(new AuthException("BOT_VERIFICATION_REQUIRED", "Ignored", 400));
        Assert.Equal(400, required.Response.StatusCode);
        var reqBody = JsonSerializer.SerializeToElement(required.Response.Body);
        Assert.Equal("BOT_VERIFICATION_REQUIRED", reqBody.GetProperty("code").GetString());
        Assert.Equal("Bot verification token is required.", reqBody.GetProperty("message").GetString());

        var failed = mapper.Map(new AuthException("BOT_VERIFICATION_FAILED", "Ignored", 400));
        Assert.Equal(400, failed.Response.StatusCode);
        var failBody = JsonSerializer.SerializeToElement(failed.Response.Body);
        Assert.Equal("BOT_VERIFICATION_FAILED", failBody.GetProperty("code").GetString());
        Assert.Equal("Bot verification failed.", failBody.GetProperty("message").GetString());

        var expired = mapper.Map(new AuthException("BOT_VERIFICATION_EXPIRED", "Ignored", 400));
        Assert.Equal(400, expired.Response.StatusCode);
        var expBody = JsonSerializer.SerializeToElement(expired.Response.Body);
        Assert.Equal("BOT_VERIFICATION_EXPIRED", expBody.GetProperty("code").GetString());
        Assert.Equal("Bot verification token has expired or was already used. Please try again.", expBody.GetProperty("message").GetString());

        var unavailable = mapper.Map(new AuthException("BOT_VERIFICATION_UNAVAILABLE", "Ignored", 503));
        Assert.Equal(503, unavailable.Response.StatusCode);
        var unavBody = JsonSerializer.SerializeToElement(unavailable.Response.Body);
        Assert.Equal("BOT_VERIFICATION_UNAVAILABLE", unavBody.GetProperty("code").GetString());
        Assert.Equal("Bot verification service is temporarily unavailable. Please retry.", unavBody.GetProperty("message").GetString());
    }

    [Theory]
    [InlineData("Production", false, "secret", new[] { "smartpark.vn" })]
    [InlineData("Production", true, "", new[] { "smartpark.vn" })]
    [InlineData("Production", true, "secret", new string[0])]
    [InlineData("Production", true, "secret", new[] { "localhost" })]
    [InlineData("Production", true, "secret", new[] { "127.0.0.1" })]
    [InlineData("Production", true, "secret", new[] { "::1" })]
    [InlineData("Production", true, "secret", new[] { "staging.smartpark.vn" })]
    [InlineData("Production", true, "secret", new[] { "https://smartpark.vn" })]
    [InlineData("Production", true, "secret", new[] { "smartpark.vn:8080" })]
    [InlineData("Production", true, "secret", new[] { "*.smartpark.vn" })]
    [InlineData("Staging", false, "secret", new[] { "staging.smartpark.vn" })]
    [InlineData("Staging", true, "", new[] { "staging.smartpark.vn" })]
    [InlineData("Staging", true, "secret", new string[0])]
    public async Task StartupGuard_RejectsInvalidEnvironmentConfigurations(
        string environment, bool enabled, string secretKey, string[] hostnames)
    {
        using var key = RSA.Create(2048);
        var config = new Dictionary<string, string?>
        {
            ["Services:Key"] = new string('k', 32),
            ["Services:Parking"] = "http://parking.test",
            ["Turnstile:Enabled"] = enabled.ToString(),
            ["Turnstile:SecretKey"] = secretKey,
            ["Workflow:KeyDirectory"] = Path.GetTempPath(),
            ["Jwt:Issuer"] = "test",
            ["Jwt:Audience"] = "test",
            ["Jwt:PrivateKeyPem"] = key.ExportRSAPrivateKeyPem()
        };
        for (var i = 0; i < Math.Max(10, hostnames.Length); i++)
        {
            config[$"Turnstile:ExpectedHostnames:{i}"] = i < hostnames.Length ? hostnames[i] : string.Empty;
        }

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await UserHost.BuildAsync(["--environment", environment], builder =>
            {
                builder.Configuration.AddInMemoryCollection(config);
            });
        });

        Assert.True(
            ex.Message.Contains("Turnstile", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("ExpectedHostnames", StringComparison.OrdinalIgnoreCase),
            $"Unexpected error message: {ex.Message}");
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public async Task StartupGuard_PermitsDisabledOrEmptyTurnstileInDevAndTest(string environment)
    {
        using var key = RSA.Create(2048);
        var config = new Dictionary<string, string?>
        {
            ["Services:Key"] = new string('k', 32),
            ["Services:Parking"] = "http://parking.test",
            ["Turnstile:Enabled"] = "false",
            ["Turnstile:SecretKey"] = "",
            ["Workflow:KeyDirectory"] = Path.GetTempPath(),
            ["Jwt:Issuer"] = "test",
            ["Jwt:Audience"] = "test",
            ["Jwt:PrivateKeyPem"] = key.ExportRSAPrivateKeyPem()
        };

        var app = await UserHost.BuildAsync(["--environment", environment], builder =>
        {
            builder.Configuration.AddInMemoryCollection(config);
        });

        Assert.NotNull(app);
        var options = app.Services.GetRequiredService<TurnstileOptions>();
        Assert.False(options.Enabled);
        var verifier = app.Services.GetRequiredService<IBotVerificationService>();
        Assert.IsType<TurnstileVerifier>(verifier);
        await app.DisposeAsync();
    }
}
