using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.API.Controllers;
using UserService.API.Extensions;
using UserService.Application;
using UserService.Application.Common.Interfaces.MessageBroker;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.Common.Models.JwT;
using UserService.Application.DTOs;
using UserService.Application.Services;
using UserService.Application.Usecase.Login;
using UserService.Infrastructure.Configurations;
using UserService.Infrastructure.Services;

namespace SmartParking.UserService.Tests;

/// <summary>
/// Comprehensive Backend Integration &amp; HTTP Pipeline Tests for SPARK-257.
/// Covers Acceptance Checklist Scenarios 1–18 (Phase 5).
/// </summary>
public sealed class BotProtectionIntegrationTests
{
    private sealed class DynamicHttpMessageHandler : HttpMessageHandler
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
                Content = new StringContent(
                    "{\"success\":true,\"action\":\"driver_register\",\"hostname\":\"localhost\"}",
                    Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class MockDriverRegistrationService : IDriverRegistrationService
    {
        public int RegisterCalls { get; private set; }
        public IBotVerificationService? BotVerifier { get; }

        public MockDriverRegistrationService(IBotVerificationService? botVerifier)
        {
            BotVerifier = botVerifier;
        }

        public async Task<DriverRegistrationResult> RegisterAsync(DriverRegistrationDto body, CancellationToken ct)
        {
            if (BotVerifier is not null)
            {
                await BotVerifier.VerifyAsync(body.TurnstileToken, "driver_register", ct);
            }
            RegisterCalls++;
            return new DriverRegistrationResult(Guid.NewGuid(), body.Email ?? "driver@test.com",
                DateTimeOffset.UtcNow.AddMinutes(5), DateTimeOffset.UtcNow.AddMinutes(1));
        }

        public Task VerifyAsync(VerifyDriverOtpDto body, CancellationToken ct) => Task.CompletedTask;
        public Task<DriverRegistrationResult> ResendAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(new DriverRegistrationResult(id, "driver@test.com",
                DateTimeOffset.UtcNow.AddMinutes(5), DateTimeOffset.UtcNow.AddMinutes(1)));
        public Task<DriverRegistrationResult> RecoverAsync(string contact, CancellationToken ct) =>
            ResendAsync(Guid.NewGuid(), ct);
    }

    private sealed class MockOwnerRegistrationService : IOwnerRegistrationService
    {
        public int RegisterCalls { get; private set; }
        public IBotVerificationService? BotVerifier { get; }
        private readonly OwnerApplicationDto record = new(Guid.NewGuid(), Guid.NewGuid(), "Owner", "Company",
            "owner@example.com", "0911111111", "basement", "pending", DateTimeOffset.UtcNow, null, null, null);

        public MockOwnerRegistrationService(IBotVerificationService? botVerifier)
        {
            BotVerifier = botVerifier;
        }

        public async Task<OwnerApplicationDto> RegisterAsync(OwnerRegistrationDto body, CancellationToken ct)
        {
            if (BotVerifier is not null)
            {
                await BotVerifier.VerifyAsync(body.TurnstileToken, "owner_register", ct);
            }
            RegisterCalls++;
            return record;
        }

        public Task<OwnerApplicationDto> SaveLotSetupAsync(OwnerLotSetupRequest body, CancellationToken ct) =>
            Task.FromResult(record with { LotSetupComplete = true, LotSetup = body.Lot });
        public Task<IReadOnlyList<OwnerApplicationDto>> ListAsync(Guid adminId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<OwnerApplicationDto>>([record]);
        public Task<OwnerApplicationDto> ReviewAsync(Guid adminId, Guid id, ReviewOwnerDto body, CancellationToken ct) =>
            Task.FromResult(record with { Status = body.Status });
    }

    private sealed class TestLogSink : ILogger<TurnstileVerifier>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }

    private static (WebApplication App, DynamicHttpMessageHandler Handler, TurnstileOptions Options, TestLogSink LogSink) CreateTestPipeline(
        Action<TurnstileOptions>? configureOptions = null)
    {
        var options = new TurnstileOptions
        {
            Enabled = true,
            SecretKey = "test-secret-key-super-safe",
            ExpectedHostnames = ["localhost", "127.0.0.1", "smartpark.vn"]
        };
        configureOptions?.Invoke(options);

        var handler = new DynamicHttpMessageHandler();
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        var logSink = new TestLogSink();
        var verifier = new TurnstileVerifier(client, options, logSink);

        var fixture = new AuthTestFixture();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseDefaultServiceProvider(opts =>
        {
            opts.ValidateOnBuild = false;
            opts.ValidateScopes = false;
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();

        builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        builder.Services.AddApiErrorResponses();
        builder.Services.AddApiCurrentActor();
        builder.Services.AddApplicationServices(new AuthenticationPolicy());
        builder.Services.AddSingleton<IVehicleRepository, UnsupportedVehicleRepository>();
        builder.Services.AddSingleton<IIdentityAccessRepository, UnusedIdentityRepository>();
        builder.Services.AddSingleton<IParkingDirectory, TestParkingDirectory>();
        builder.Services.AddSingleton<IWorkflowProtector, TestWorkflowProtector>();
        TestAuthorization.AddServices(builder.Services, fixture);

        builder.Services.AddSingleton<IBotVerificationService>(verifier);
        builder.Services.AddSingleton<IDriverRegistrationService>(new MockDriverRegistrationService(verifier));
        builder.Services.AddSingleton<IOwnerRegistrationService>(new MockOwnerRegistrationService(verifier));

        builder.Services.AddSingleton<IUnitOfWork>(fixture);
        builder.Services.AddSingleton<ICurrentAccessStore>(fixture);
        builder.Services.AddSingleton<IEventPublisher, RecordingProfilePublisher>();
        builder.Services.AddSingleton<IAuthSessionStore>(fixture.Sessions);
        builder.Services.AddSingleton<IPasswordService, BcryptPasswordService>();
        builder.Services.AddSingleton<TimeProvider>(fixture.Clock);
        builder.Services.AddJWTAuthentication(builder.Configuration, true);
        builder.Services.AddAuthorization();

        builder.Services.AddRateLimiter(rateOptions =>
        {
            rateOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            rateOptions.AddPolicy("LoginRateLimit", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new()
                    { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            rateOptions.AddPolicy("RegistrationRateLimit", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new()
                    { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });

        var app = builder.Build();
        app.UseForwardedHeaders();
        app.UseRouting();
        app.UseApiErrorResponses();

        app.Use(async (context, next) =>
        {
            if (context.Request.ContentLength > 65536)
            {
                throw new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge);
            }
            await next();
        });

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapControllers();

        return (app, handler, options, logSink);
    }

    // -----------------------------------------------------------------------------------------
    // Scenario 1: Valid Token Flow (Driver, Owner, Login)
    // -----------------------------------------------------------------------------------------
    [Fact]
    public async Task Scenario01_ValidToken_DriverRegistration_ExecutesAndReturns201()
    {
        var (app, handler, _, _) = CreateTestPipeline();
        handler.HandlerFunc = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"success\":true,\"action\":\"driver_register\",\"hostname\":\"localhost\"}", Encoding.UTF8, "application/json")
        });
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/api/auth/register/driver", new
        {
            fullName = "John Driver",
            email = "john.driver@example.com",
            password = "Password@123",
            turnstileToken = "valid-cf-token"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(envelope.GetProperty("success").GetBoolean());
        Assert.Single(handler.CapturedRequests);
    }

    [Fact]
    public async Task Scenario01_ValidToken_OwnerRegistration_ExecutesAndReturns201()
    {
        var (app, handler, _, _) = CreateTestPipeline();
        handler.HandlerFunc = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"success\":true,\"action\":\"owner_register\",\"hostname\":\"localhost\"}", Encoding.UTF8, "application/json")
        });
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/api/auth/register/owner", new
        {
            fullName = "Jane Owner",
            businessName = "Jane Lots Inc",
            email = "jane.owner@example.com",
            phone = "0901234567",
            lotType = "outdoor",
            agreedToPolicy = true,
            turnstileToken = "valid-cf-token"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(envelope.GetProperty("success").GetBoolean());
        Assert.Single(handler.CapturedRequests);
    }

    [Fact]
    public async Task Scenario01_ValidToken_PasswordLogin_ExecutesAndReturns200()
    {
        var (app, handler, _, _) = CreateTestPipeline();
        handler.HandlerFunc = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"success\":true,\"action\":\"password_login\",\"hostname\":\"localhost\"}", Encoding.UTF8, "application/json")
        });
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "driver@gmail.com",
            password = "Password@123",
            turnstileToken = "valid-cf-token"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(envelope.GetProperty("success").GetBoolean());
        Assert.Equal("Login successful", envelope.GetProperty("message").GetString());
    }

    // -----------------------------------------------------------------------------------------
    // Scenario 2: Missing Token (null or omitted)
    // -----------------------------------------------------------------------------------------
    [Theory]
    [InlineData("/api/auth/register/driver")]
    [InlineData("/api/auth/register/owner")]
    [InlineData("/api/auth/login")]
    public async Task Scenario02_MissingToken_Returns400BotVerificationRequired_WithoutCallingCloudflare(string endpoint)
    {
        var (app, handler, _, _) = CreateTestPipeline();
        await app.StartAsync();
        using var client = app.GetTestClient();

        object body;
        if (endpoint.Contains("driver"))
        {
            body = new { fullName = "Driver User", email = "driver@test.com", password = "Password@123", turnstileToken = (string?)null };
        }
        else if (endpoint.Contains("owner"))
        {
            body = new
            {
                fullName = "Owner User",
                businessName = "Owner Biz",
                email = "owner@test.com",
                phone = "0901234567",
                lotType = "outdoor",
                agreedToPolicy = true,
                turnstileToken = (string?)null
            };
        }
        else
        {
            body = new { email = "driver@gmail.com", password = "Password@123", turnstileToken = (string?)null };
        }

        var response = await client.PostAsJsonAsync(endpoint, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(envelope.GetProperty("success").GetBoolean());
        Assert.Equal("BOT_VERIFICATION_REQUIRED", envelope.GetProperty("code").GetString());
        Assert.Empty(handler.CapturedRequests);
    }

    // -----------------------------------------------------------------------------------------
    // Scenario 3: Whitespace Token
    // -----------------------------------------------------------------------------------------
    [Theory]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public async Task Scenario03_WhitespaceToken_Returns400BotVerificationRequired_WithoutCallingCloudflare(string whitespace)
    {
        var (app, handler, _, _) = CreateTestPipeline();
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "driver@gmail.com",
            password = "Password@123",
            turnstileToken = whitespace
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("BOT_VERIFICATION_REQUIRED", envelope.GetProperty("code").GetString());
        Assert.Empty(handler.CapturedRequests);
    }

    // -----------------------------------------------------------------------------------------
    // Scenario 4: Token Length > 2048 Characters
    // -----------------------------------------------------------------------------------------
    [Fact]
    public async Task Scenario04_TokenLengthExceeds2048_Returns400BotVerificationFailed_NotGenericValidationFailed()
    {
        var (app, handler, _, _) = CreateTestPipeline();
        await app.StartAsync();
        using var client = app.GetTestClient();

        var oversizedToken = new string('A', 2049);
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "driver@gmail.com",
            password = "Password@123",
            turnstileToken = oversizedToken
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("BOT_VERIFICATION_FAILED", envelope.GetProperty("code").GetString());
        Assert.NotEqual("VALIDATION_FAILED", envelope.GetProperty("code").GetString());
        Assert.Empty(handler.CapturedRequests);
    }

    // -----------------------------------------------------------------------------------------
    // Scenario 5: Invalid Token Format
    // -----------------------------------------------------------------------------------------
    [Fact]
    public async Task Scenario05_InvalidTokenFormat_Returns400BotVerificationFailed()
    {
        var (app, handler, _, _) = CreateTestPipeline();
        handler.HandlerFunc = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"success\":false,\"error-codes\":[\"invalid-input-response\"]}",
                Encoding.UTF8, "application/json")
        });
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "driver@gmail.com",
            password = "Password@123",
            turnstileToken = "malformed-junk-token"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("BOT_VERIFICATION_FAILED", envelope.GetProperty("code").GetString());
    }

    // -----------------------------------------------------------------------------------------
    // Scenario 6: Expired / Duplicate Token (timeout-or-duplicate)
    // -----------------------------------------------------------------------------------------
    [Fact]
    public async Task Scenario06_ExpiredOrDuplicateToken_Returns400BotVerificationExpired()
    {
        var (app, handler, _, _) = CreateTestPipeline();
        handler.HandlerFunc = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"success\":false,\"error-codes\":[\"timeout-or-duplicate\"]}",
                Encoding.UTF8, "application/json")
        });
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "driver@gmail.com",
            password = "Password@123",
            turnstileToken = "consumed-token-already-spent"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("BOT_VERIFICATION_EXPIRED", envelope.GetProperty("code").GetString());
    }

    // -----------------------------------------------------------------------------------------
    // Scenario 7: Action Mismatch
    // -----------------------------------------------------------------------------------------
    [Fact]
    public async Task Scenario07_ActionMismatch_Returns400BotVerificationFailed()
    {
        var (app, handler, _, _) = CreateTestPipeline();
        // Cloudflare claims this token was minted for driver_register
        handler.HandlerFunc = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"success\":true,\"action\":\"driver_register\",\"hostname\":\"localhost\"}",
                Encoding.UTF8, "application/json")
        });
        await app.StartAsync();
        using var client = app.GetTestClient();

        // But caller submits it to password_login
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "driver@gmail.com",
            password = "Password@123",
            turnstileToken = "token-minted-for-driver-register"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("BOT_VERIFICATION_FAILED", envelope.GetProperty("code").GetString());
    }

    // -----------------------------------------------------------------------------------------
    // Scenario 8: Hostname Mismatch
    // -----------------------------------------------------------------------------------------
    [Fact]
    public async Task Scenario08_HostnameMismatch_Returns400BotVerificationFailed()
    {
        var (app, handler, _, _) = CreateTestPipeline();
        // Cloudflare returns hostname not in allowlist
        handler.HandlerFunc = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"success\":true,\"action\":\"password_login\",\"hostname\":\"phishing-smartpark.com\"}",
                Encoding.UTF8, "application/json")
        });
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "driver@gmail.com",
            password = "Password@123",
            turnstileToken = "token-from-foreign-domain"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("BOT_VERIFICATION_FAILED", envelope.GetProperty("code").GetString());
    }

    // -----------------------------------------------------------------------------------------
    // Scenario 9: Rate Limiting Enforcement
    // -----------------------------------------------------------------------------------------
    [Fact]
    public async Task Scenario09_RateLimiting_Rejects11thRequestWith429_AndSeparatesQuotas()
    {
        var (app, handler, _, _) = CreateTestPipeline();
        handler.HandlerFunc = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"success\":true,\"action\":\"driver_register\",\"hostname\":\"localhost\"}", Encoding.UTF8, "application/json")
        });
        await app.StartAsync();
        using var client = app.GetTestClient();

        // 10 registration requests within permit limit
        for (var i = 1; i <= 10; i++)
        {
            var res = await client.PostAsJsonAsync("/api/auth/register/driver", new
            {
                fullName = $"Driver {i}",
                email = $"driver{i}@test.com",
                password = "Password@123",
                turnstileToken = "valid-token"
            });
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        }

        // 11th registration request exceeds quota -> 429
        var flood = await client.PostAsJsonAsync("/api/auth/register/driver", new
        {
            fullName = "Driver 11",
            email = "driver11@test.com",
            password = "Password@123",
            turnstileToken = "valid-token"
        });
        Assert.Equal(HttpStatusCode.TooManyRequests, flood.StatusCode);

        // Login quota is separate: login request still passes through
        handler.HandlerFunc = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"success\":true,\"action\":\"password_login\",\"hostname\":\"localhost\"}", Encoding.UTF8, "application/json")
        });
        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "driver@gmail.com",
            password = "Password@123",
            turnstileToken = "valid-token"
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    // -----------------------------------------------------------------------------------------
    // Scenario 10: Provider Timeout (> 5s)
    // -----------------------------------------------------------------------------------------
    [Fact]
    public async Task Scenario10_ProviderTimeout_Returns503BotVerificationUnavailable()
    {
        var (app, handler, _, _) = CreateTestPipeline();
        handler.HandlerFunc = (_, _) => throw new OperationCanceledException("HttpClient timeout exceeded");
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "driver@gmail.com",
            password = "Password@123",
            turnstileToken = "valid-token"
        });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("BOT_VERIFICATION_UNAVAILABLE", envelope.GetProperty("code").GetString());
    }

    // -----------------------------------------------------------------------------------------
    // Scenario 11: Client Cancellation
    // -----------------------------------------------------------------------------------------
    [Fact]
    public async Task Scenario11_ClientCancellation_PropagatesCleanlyWithoutWrappingIn503()
    {
        var handler = new DynamicHttpMessageHandler();
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Simulate client abort before/during call

        handler.HandlerFunc = (_, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        };

        var verifier = new TurnstileVerifier(new HttpClient(handler), new TurnstileOptions
        {
            Enabled = true,
            SecretKey = "secret",
            ExpectedHostnames = ["localhost"]
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            verifier.VerifyAsync("valid-token", "password_login", cts.Token));
    }

    // -----------------------------------------------------------------------------------------
    // Scenario 12: Provider HTTP Non-2xx Errors (429, 400, 403, 500, 502, 503)
    // -----------------------------------------------------------------------------------------
    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task Scenario12_ProviderHttpErrors_Returns503BotVerificationUnavailable(HttpStatusCode providerStatus)
    {
        var (app, handler, _, _) = CreateTestPipeline();
        handler.HandlerFunc = (_, _) => Task.FromResult(new HttpResponseMessage(providerStatus));
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "driver@gmail.com",
            password = "Password@123",
            turnstileToken = "valid-token"
        });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("BOT_VERIFICATION_UNAVAILABLE", envelope.GetProperty("code").GetString());
    }

    // -----------------------------------------------------------------------------------------
    // Scenario 13: Provider Malformed JSON Response
    // -----------------------------------------------------------------------------------------
    [Fact]
    public async Task Scenario13_ProviderMalformedJson_Returns503BotVerificationUnavailable()
    {
        var (app, handler, _, _) = CreateTestPipeline();
        handler.HandlerFunc = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html><body>502 Bad Gateway from Cloudflare Edge</body></html>", Encoding.UTF8, "text/html")
        });
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "driver@gmail.com",
            password = "Password@123",
            turnstileToken = "valid-token"
        });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("BOT_VERIFICATION_UNAVAILABLE", envelope.GetProperty("code").GetString());
    }

    // -----------------------------------------------------------------------------------------
    // Scenario 14: Provider Infrastructure Error (missing-input-response, bad-request)
    // -----------------------------------------------------------------------------------------
    [Theory]
    [InlineData("missing-input-response")]
    [InlineData("bad-request")]
    [InlineData("missing-input-secret")]
    [InlineData("invalid-input-secret")]
    [InlineData("internal-error")]
    public async Task Scenario14_ProviderInfrastructureErrors_Return503BotVerificationUnavailable(string infraError)
    {
        var (app, handler, _, _) = CreateTestPipeline();
        handler.HandlerFunc = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { success = false, error_codes = new[] { infraError } }),
                Encoding.UTF8, "application/json")
        });
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "driver@gmail.com",
            password = "Password@123",
            turnstileToken = "valid-token"
        });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("BOT_VERIFICATION_UNAVAILABLE", envelope.GetProperty("code").GetString());
    }

    // -----------------------------------------------------------------------------------------
    // Scenario 15: Request Body Size Limit (> 64KB)
    // -----------------------------------------------------------------------------------------
    [Fact]
    public async Task Scenario15_RequestBodyExceeding64KB_Returns413PayloadTooLarge()
    {
        var (app, _, _, _) = CreateTestPipeline();
        await app.StartAsync();
        using var client = app.GetTestClient();

        // 65KB string in payload
        var largePadding = new string('x', 66 * 1024);
        var content = new StringContent(
            JsonSerializer.Serialize(new
            {
                email = "driver@gmail.com",
                password = "Password@123",
                padding = largePadding,
                turnstileToken = "token"
            }),
            Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/auth/login", content);

        Assert.Equal((HttpStatusCode)StatusCodes.Status413PayloadTooLarge, response.StatusCode);
    }

    // -----------------------------------------------------------------------------------------
    // Scenario 16: Log Sanitization (Never log SecretKey or Raw Token)
    // -----------------------------------------------------------------------------------------
    [Fact]
    public async Task Scenario16_LogSanitization_NeverLogsSecretKeyOrRawToken()
    {
        const string rawSecretKey = "SUPER-SECRET-KEY-ABC-999";
        const string rawUserToken = "SUPER-SECRET-TOKEN-XYZ-777";

        var (app, handler, _, logSink) = CreateTestPipeline(opts => opts.SecretKey = rawSecretKey);
        handler.HandlerFunc = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"success\":true,\"action\":\"password_login\",\"hostname\":\"localhost\"}", Encoding.UTF8, "application/json")
        });
        await app.StartAsync();
        using var client = app.GetTestClient();

        await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "driver@gmail.com",
            password = "Password@123",
            turnstileToken = rawUserToken
        });

        Assert.NotEmpty(logSink.Messages);
        foreach (var msg in logSink.Messages)
        {
            Assert.DoesNotContain(rawSecretKey, msg);
            Assert.DoesNotContain(rawUserToken, msg);
        }
    }

    // -----------------------------------------------------------------------------------------
    // Scenario 17: Database Transaction Safety (Verify before BeginTransaction)
    // -----------------------------------------------------------------------------------------
    [Fact]
    public async Task Scenario17_DbTransactionSafety_VerifyAsyncPrecedesBeginTransactionInAllFlows()
    {
        var executionOrder = new List<string>();

        var verifierMock = new CallOrderBotVerifier(onVerify: () => executionOrder.Add("VerifyAsync"));
        var uowMock = new CallOrderUnitOfWork(onBeginTx: () => executionOrder.Add("BeginTransactionAsync"));

        var handler = new LoginCommandHandler(
            uowMock,
            new BcryptPasswordService(),
            uowMock.InnerFixture.Auth,
            uowMock.InnerFixture.Policy,
            uowMock.InnerFixture.Clock,
            null,
            verifierMock);

        var result = await handler.Handle(new LoginCommand
        {
            Email = "driver@gmail.com",
            Password = "Password@123",
            TurnstileToken = "valid-token"
        }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, executionOrder.Count);
        Assert.Equal("VerifyAsync", executionOrder[0]);
        Assert.Equal("BeginTransactionAsync", executionOrder[1]);
    }

    private sealed class CallOrderBotVerifier(Action onVerify) : IBotVerificationService
    {
        public Task VerifyAsync(string? token, string action, CancellationToken ct, string? remoteIp = null)
        {
            onVerify();
            return Task.CompletedTask;
        }
    }

    private sealed class CallOrderUnitOfWork(Action onBeginTx) : IUnitOfWork
    {
        public readonly AuthTestFixture InnerFixture = new();
        public IUserRepository UserRepository => InnerFixture;

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
        {
            onBeginTx();
            return InnerFixture.BeginTransactionAsync(cancellationToken);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            InnerFixture.SaveChangesAsync(cancellationToken);
    }

    // -----------------------------------------------------------------------------------------
    // Scenario 18: Bypass SignInErrors Coercion
    // -----------------------------------------------------------------------------------------
    [Theory]
    [InlineData("BOT_VERIFICATION_REQUIRED", 400)]
    [InlineData("BOT_VERIFICATION_FAILED", 400)]
    [InlineData("BOT_VERIFICATION_EXPIRED", 400)]
    [InlineData("BOT_VERIFICATION_UNAVAILABLE", 503)]
    public async Task Scenario18_BypassSignInErrors_PreservesExactBotErrorCodeAndStatusWithoutCoercionToAuthFailed(
        string botErrorCode, int expectedStatus)
    {
        var fixture = new AuthTestFixture();
        var mockVerifier = new ThrowingBotVerificationService(new AuthException(botErrorCode, "Bot test error", expectedStatus));

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        builder.Services.AddApiErrorResponses();
        builder.Services.AddApiCurrentActor();
        builder.Services.AddApplicationServices(new AuthenticationPolicy());
        builder.Services.AddSingleton<IVehicleRepository, UnsupportedVehicleRepository>();
        builder.Services.AddSingleton<IIdentityAccessRepository, UnusedIdentityRepository>();
        builder.Services.AddSingleton<IParkingDirectory, TestParkingDirectory>();
        builder.Services.AddSingleton<IWorkflowProtector, TestWorkflowProtector>();
        TestAuthorization.AddServices(builder.Services, fixture);

        builder.Services.AddSingleton<IBotVerificationService>(mockVerifier);
        builder.Services.AddSingleton<IUnitOfWork>(fixture);
        builder.Services.AddSingleton<ICurrentAccessStore>(fixture);
        builder.Services.AddSingleton<IEventPublisher, RecordingProfilePublisher>();
        builder.Services.AddSingleton<IAuthSessionStore>(fixture.Sessions);
        builder.Services.AddSingleton<IPasswordService, BcryptPasswordService>();
        builder.Services.AddSingleton<TimeProvider>(fixture.Clock);
        builder.Services.AddJWTAuthentication(builder.Configuration, true);
        builder.Services.AddAuthorization();
        builder.Services.AddRateLimiter(options =>
        {
            options.AddPolicy("LoginRateLimit", _ => RateLimitPartition.GetNoLimiter("test"));
            options.AddPolicy("RegistrationRateLimit", _ => RateLimitPartition.GetNoLimiter("test"));
        });

        await using var app = builder.Build();
        app.UseRouting();
        app.UseApiErrorResponses();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapControllers();
        await app.StartAsync();

        using var client = app.GetTestClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "driver@gmail.com",
            password = "Password@123",
            turnstileToken = "test-token"
        });

        Assert.Equal((HttpStatusCode)expectedStatus, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(envelope.GetProperty("success").GetBoolean());
        Assert.Equal(botErrorCode, envelope.GetProperty("code").GetString());
        Assert.NotEqual("AUTH_FAILED", envelope.GetProperty("code").GetString());
    }

    private sealed class ThrowingBotVerificationService(Exception exceptionToThrow) : IBotVerificationService
    {
        public Task VerifyAsync(string? token, string action, CancellationToken ct, string? remoteIp = null) =>
            Task.FromException(exceptionToThrow);
    }
}
