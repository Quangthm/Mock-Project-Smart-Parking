using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using UserService.API.Common;
using UserService.API.Extensions;
using UserService.API.Middleware;
using UserService.Application.Common.Behaviors;
using UserService.Application.Common.Models.Exceptions;

namespace SmartParking.UserService.Tests;

public sealed class ApiErrorHandlingTests
{
    [Theory]
    [InlineData("/missing", "GET", null, 404, "NOT_FOUND")]
    [InlineData("/error-probe/binding", "GET", null, 405, "METHOD_NOT_ALLOWED")]
    [InlineData("/error-probe/binding", "POST", "text/plain", 415, "UNSUPPORTED_MEDIA_TYPE")]
    [InlineData("/error-probe/binding", "POST", "application/json", 400, "VALIDATION_FAILED")]
    [InlineData("/error-probe/secure", "GET", null, 401, "INVALID_TOKEN")]
    [InlineData("/error-probe/value-failure", "GET", null, 409, "PLATE_EXISTS")]
    [InlineData("/error-probe/conflict", "GET", null, 409, "PROFILE_ACCESS_CONFLICT")]
    public async Task FrameworkAndApplicationFailuresUseApprovedStatusAndEnvelope(
        string route, string method, string? contentType, int status, string code)
    {
        await using var host = await ErrorHost.StartAsync();
        using var request = new HttpRequestMessage(new HttpMethod(method), route);
        if (contentType is not null)
            request.Content = new StringContent("{\"name\":", Encoding.UTF8, contentType);
        using var response = await host.Client.SendAsync(request);
        await AssertErrorAsync(response, status, code);
        if (status == 401)
        {
            Assert.Equal("Bearer", Assert.Single(response.Headers.WwwAuthenticate).Scheme);
            Assert.Equal(1, host.Events.Challenges);
        }
        if (status == 405)
            Assert.Contains("POST", response.Content.Headers.Allow);
    }

    [Fact]
    public async Task ValidBearerForbidAndInvalidBearerChallengeRetainExistingEvents()
    {
        await using var host = await ErrorHost.StartAsync();
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", "secret-invalid-token");
        using (var challenge = await host.Client.GetAsync("/error-probe/secure"))
            await AssertErrorAsync(challenge, 401, "INVALID_TOKEN");
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", host.CreateToken());
        using var forbid = await host.Client.GetAsync("/error-probe/secure");
        await AssertErrorAsync(forbid, 403, "FORBIDDEN");
        Assert.Equal(1, host.Events.Validated);
        Assert.Equal(1, host.Events.Forbidden);
        Assert.Equal(1, host.Events.Challenges);
    }

    [Fact]
    public async Task AlreadyHandledAuthenticationCallbacksAreNotOverwritten()
    {
        await using var host = await ErrorHost.StartAsync(completeAuthenticationResponses: true);
        using var challenge = await host.Client.GetAsync("/error-probe/secure");
        Assert.Equal(HttpStatusCode.Unauthorized, challenge.StatusCode);
        Assert.Equal("custom challenge", await challenge.Content.ReadAsStringAsync());
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", host.CreateToken());
        using var forbid = await host.Client.GetAsync("/error-probe/secure");
        Assert.Equal(HttpStatusCode.Forbidden, forbid.StatusCode);
        Assert.Equal("custom forbid", await forbid.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RateLimiterRejectionUsesSharedWriter()
    {
        await using var host = await ErrorHost.StartAsync();
        using var first = await host.Client.GetAsync("/error-probe/limited");
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        using var second = await host.Client.GetAsync("/error-probe/limited");
        await AssertErrorAsync(second, 429, "TOO_MANY_REQUESTS");
    }

    [Theory]
    [InlineData("{\"name\":", 0)]
    [InlineData("{\"name\":123}", 0)]
    [InlineData("{\"name\":\"\"}", 1)]
    public async Task BindingAndRealMediatorValidationRejectBeforeHandler(string json, int validatorCalls)
    {
        await using var host = await ErrorHost.StartAsync();
        using var response = await host.Client.PostAsync("/error-probe/binding",
            new StringContent(json, Encoding.UTF8, "application/json"));
        await AssertErrorAsync(response, 400, "VALIDATION_FAILED");
        Assert.Equal(validatorCalls, host.Events.ValidatedInputs);
        Assert.Equal(0, host.Events.HandledInputs);
    }

    [Fact]
    public async Task UnexpectedDatabaseErrorIsSafe500LoggedOnceWithMetadataOnly()
    {
        await using var host = await ErrorHost.StartAsync();
        using var response = await host.Client.GetAsync("/error-probe/unexpected/secret-route-value?password=secret-query");
        await AssertErrorAsync(response, 500, "INTERNAL_ERROR");
        var errors = host.Logs.Entries.Where(e => e.Level == LogLevel.Error).ToArray();
        var error = Assert.Single(errors);
        Assert.Equal(typeof(ApiExceptionHandler).FullName, error.Category);
        Assert.Null(error.Exception);
        var completion = Assert.Single(host.Logs.Entries, e => e.Category == typeof(RequestCompletionMiddleware).FullName);
        Assert.Contains("error-probe/unexpected/{id}", completion.Message);
        Assert.Contains("status 500", completion.Message);
        Assert.Contains("correlation", completion.Message);
        Assert.DoesNotContain("secret", string.Join(" ", host.Logs.Entries.Select(e => e.Message)), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HeadAndNoContentHaveNoBodyAndFilesAndStreamsAreUnchanged()
    {
        await using var host = await ErrorHost.StartAsync();
        using var head = await host.Client.SendAsync(new(HttpMethod.Head, "/missing"));
        Assert.Equal(HttpStatusCode.NotFound, head.StatusCode);
        Assert.Empty(await head.Content.ReadAsByteArrayAsync());
        using var failedHead = await host.Client.SendAsync(new(HttpMethod.Head, "/head-failure"));
        Assert.Equal(HttpStatusCode.InternalServerError, failedHead.StatusCode);
        Assert.Empty(await failedHead.Content.ReadAsByteArrayAsync());
        using var noContent = await host.Client.GetAsync("/error-probe/empty");
        Assert.Equal(HttpStatusCode.NoContent, noContent.StatusCode);
        Assert.Empty(await noContent.Content.ReadAsByteArrayAsync());
        using var file = await host.Client.GetAsync("/error-probe/file");
        Assert.Equal("image/png", file.Content.Headers.ContentType?.MediaType);
        Assert.Equal(new byte[] { 137, 80, 78, 71 }, await file.Content.ReadAsByteArrayAsync());
        using var stream = await host.Client.GetAsync("/stream");
        Assert.Equal("text/event-stream", stream.Content.Headers.ContentType?.MediaType);
        Assert.Equal("data: first\n\ndata: second\n\n", await stream.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ResponseStartedFailurePropagatesWithoutAppendingAnErrorDocument()
    {
        await using var host = await ErrorHost.StartAsync();
        var exception = await Record.ExceptionAsync(async () =>
        {
            using var response = await host.Client.GetAsync("/started", HttpCompletionOption.ResponseHeadersRead);
            await response.Content.ReadAsStringAsync();
        });
        Assert.NotNull(exception);
        Assert.DoesNotContain(host.Logs.Entries, e => e.Category == typeof(ApiExceptionHandler).FullName);
        Assert.Contains(host.Logs.Entries, e => e.Category == typeof(RequestCompletionMiddleware).FullName
            && e.Message.Contains("propagated failure True", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RequestCancellationHasNoGeneric500OrUnexpectedDiagnostic()
    {
        await using var host = await ErrorHost.StartAsync();
        using var response = await host.Client.GetAsync("/cancelled");
        Assert.Equal(499, (int)response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        Assert.DoesNotContain(host.Logs.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task CancellationWithoutRequestAbortRemainsAnUnexpectedServerFailure()
    {
        await using var host = await ErrorHost.StartAsync();
        using var response = await host.Client.GetAsync("/unexpected-cancellation");
        await AssertErrorAsync(response, 500, "INTERNAL_ERROR");
        Assert.Single(host.Logs.Entries, entry => entry.Level == LogLevel.Error);
    }

    [Fact]
    public void RegistryRejectsDuplicateTypesAndUsesExactMatchingWithSafeFallback()
    {
        var mapping = new KeyValuePair<Type, Func<Exception, ErrorHandlingResult>>(typeof(ArgumentException),
            _ => new(ApiResponseFactory.Error("EXAMPLE", "Example", 400)));
        Assert.Throws<ArgumentException>(() => new ApiExceptionMapper([mapping, mapping]));
        var mapper = new ApiExceptionMapper([mapping]);
        Assert.Equal(400, mapper.Map(new ArgumentException()).Response.StatusCode);
        Assert.True(mapper.Map(new ArgumentNullException("secret")).IsUnexpected);
        Assert.True(ApiExceptionMapper.CreateDefault().Map(new AuthException("UNREGISTERED", "secret", 400)).IsUnexpected);
        // A filesystem permission failure is not evidence of an invalid JWT.
        Assert.True(ApiExceptionMapper.CreateDefault().Map(new UnauthorizedAccessException("secret")).IsUnexpected);
        Assert.True(ApiExceptionMapper.CreateDefault().Map(new AuthException("FORBIDDEN", "secret", 200)).IsUnexpected);
        var safe = ApiExceptionMapper.CreateDefault().Map(new AuthException("FORBIDDEN", "secret", 403));
        Assert.Equal(403, safe.Response.StatusCode);
        Assert.DoesNotContain("secret", JsonSerializer.Serialize(safe.Response.Body));
        var badRequest = ApiExceptionMapper.CreateDefault().Map(new BadHttpRequestException("secret", 415));
        Assert.Equal(415, badRequest.Response.StatusCode);
    }

    [Theory]
    [InlineData("CONTACT_UNVERIFIED", 401)]
    [InlineData("CONTACT_UNVERIFIED", 409)]
    [InlineData("CONTACT_EXISTS", 409)]
    [InlineData("EMAIL_EXISTS", 409)]
    [InlineData("REGISTRATION_NOT_FOUND", 404)]
    [InlineData("REGISTRATION_CLOSED", 409)]
    [InlineData("CHALLENGE_CLOSED", 409)]
    [InlineData("OTP_LOCKED", 423)]
    [InlineData("OTP_COOLDOWN", 429)]
    [InlineData("OTP_EXPIRED", 400)]
    [InlineData("OTP_INVALID", 400)]
    [InlineData("MFA_ALREADY_ENABLED", 409)]
    [InlineData("MFA_INVALID", 400)]
    [InlineData("DELIVERY_NOT_FOUND", 404)]
    [InlineData("APPLICATION_NOT_FOUND", 404)]
    [InlineData("APPLICATION_CLOSED", 409)]
    [InlineData("SITE_ACCESS_DENIED", 403)]
    [InlineData("USER_NOT_FOUND", 404)]
    [InlineData("ADMIN_ACCOUNT_PROTECTED", 409)]
    [InlineData("INVALID_STATUS_TRANSITION", 409)]
    [InlineData("PARKING_UNAVAILABLE", 503)]
    public void ExistingWorkflowErrorsRetainKnownStatusWithoutExposingExceptionText(string code, int status)
    {
        var mapped = ApiExceptionMapper.CreateDefault().Map(new AuthException(code, "secret SQL password", status));
        Assert.Equal(status, mapped.Response.StatusCode);
        var body = JsonSerializer.SerializeToElement(mapped.Response.Body);
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.DoesNotContain("secret", body.ToString());
    }

    private static async Task AssertErrorAsync(HttpResponseMessage response, int status, string code)
    {
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var text = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(text);
        var body = document.RootElement;
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.True(body.TryGetProperty("message", out _));
        Assert.False(body.TryGetProperty("status", out _));
        Assert.False(body.TryGetProperty("statusCode", out _));
        Assert.False(body.TryGetProperty("title", out _));
        Assert.DoesNotContain("secret", text, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ErrorHost(WebApplication app, ProbeEvents events, CapturedLogs logs) : IAsyncDisposable
    {
        private static readonly SymmetricSecurityKey Key = new(Encoding.UTF8.GetBytes("isolated-http-test-signing-key-at-least-32-bytes"));
        public HttpClient Client { get; } = app.GetTestClient();
        public ProbeEvents Events { get; } = events;
        public CapturedLogs Logs { get; } = logs;
        public string CreateToken() => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: "error-test", audience: "error-test", claims: [new Claim("role", "Driver")],
            expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(Key, SecurityAlgorithms.HmacSha256)));

        public static async Task<ErrorHost> StartAsync(bool completeAuthenticationResponses = false)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            builder.WebHost.UseTestServer();
            var logs = new CapturedLogs();
            // Match the service's production logging levels: framework Information
            // hosting logs include URL values; our completion record uses templates.
            builder.Logging.ClearProviders().AddProvider(logs)
                .SetMinimumLevel(LogLevel.Information)
                .AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
            var events = new ProbeEvents();
            builder.Services.AddSingleton(events);
            Microsoft.AspNetCore.DataProtection.DataProtectionBuilderExtensions.UseEphemeralDataProtectionProvider(
                builder.Services.AddDataProtection());
            builder.Services.AddControllers().AddApplicationPart(typeof(ErrorProbeController).Assembly);
            builder.Services.AddApiErrorResponses();
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new()
                {
                    ValidateIssuer = true, ValidIssuer = "error-test", ValidateAudience = true, ValidAudience = "error-test",
                    ValidateIssuerSigningKey = true, IssuerSigningKey = Key, ValidateLifetime = true, RoleClaimType = "role"
                };
                options.Events = new()
                {
                    OnTokenValidated = _ => { Interlocked.Increment(ref events.Validated); return Task.CompletedTask; },
                    OnChallenge = async context =>
                    {
                        Interlocked.Increment(ref events.Challenges);
                        if (completeAuthenticationResponses)
                        {
                            context.HandleResponse();
                            context.Response.StatusCode = 401;
                            await context.Response.WriteAsync("custom challenge");
                        }
                    },
                    OnForbidden = async context =>
                    {
                        Interlocked.Increment(ref events.Forbidden);
                        if (completeAuthenticationResponses)
                        {
                            context.Response.StatusCode = 403;
                            await context.Response.WriteAsync("custom forbid");
                        }
                    }
                };
            });
            builder.Services.AddAuthorization();
            builder.Services.AddMediatR(config => config.RegisterServicesFromAssemblyContaining<ErrorProbeCommand>());
            builder.Services.AddTransient<IValidator<ErrorProbeCommand>, ErrorProbeValidator>();
            builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = 429;
                options.AddFixedWindowLimiter("probe", limiter =>
                {
                    limiter.PermitLimit = 1; limiter.Window = TimeSpan.FromHours(1); limiter.QueueLimit = 0;
                });
            });
            // The .NET 10 key-ring warmup service is independent of the ephemeral
            // IDataProtectionProvider. This JWT-only test host must not open developer keys.
            foreach (var descriptor in builder.Services.Where(service => service.ImplementationType?.FullName ==
                "Microsoft.AspNetCore.DataProtection.Internal.DataProtectionHostedService").ToArray())
                builder.Services.Remove(descriptor);
            var app = builder.Build();
            app.UseRouting();
            app.UseApiErrorResponses();
            app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter();
            app.MapControllers();
            app.MapGet("/stream", async context =>
            {
                context.Response.ContentType = "text/event-stream";
                await context.Response.WriteAsync("data: first\n\n");
                await context.Response.Body.FlushAsync();
                await context.Response.WriteAsync("data: second\n\n");
            });
            app.MapGet("/started", async context =>
            {
                await context.Response.WriteAsync("partial");
                await context.Response.Body.FlushAsync();
                throw new InvalidOperationException("Response already started.");
            });
            app.MapGet("/cancelled", (HttpContext context) =>
            {
                context.RequestAborted = new CancellationToken(canceled: true);
                throw new OperationCanceledException(context.RequestAborted);
            });
            app.MapGet("/unexpected-cancellation", (HttpContext _) => throw new OperationCanceledException("secret"));
            app.MapMethods("/head-failure", ["HEAD"], (HttpContext _) => throw new InvalidOperationException("secret"));
            await app.StartAsync();
            return new ErrorHost(app, events, logs);
        }

        public async ValueTask DisposeAsync() { Client.Dispose(); await app.DisposeAsync(); }
    }

    public sealed class ProbeEvents
    {
        public int Challenges;
        public int Forbidden;
        public int Validated;
        public int ValidatedInputs;
        public int HandledInputs;
    }

    private sealed class CapturedLogs : ILoggerProvider
    {
        public ConcurrentQueue<LogEntry> Entries { get; } = new();
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(categoryName, Entries);
        public void Dispose() { }
        private sealed class CaptureLogger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel level) => true;
            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) => entries.Enqueue(new(category, level, formatter(state, exception), exception));
        }
    }
    private sealed record LogEntry(string Category, LogLevel Level, string Message, Exception? Exception);
}

public sealed record ErrorProbeInput(string? Name);
public sealed record ErrorProbeCommand(string? Name) : IRequest<string>;
public sealed class ErrorProbeValidator : AbstractValidator<ErrorProbeCommand>
{
    public ErrorProbeValidator(ApiErrorHandlingTests.ProbeEvents events)
    {
        RuleFor(request => request.Name).MustAsync(async (name, cancellationToken) =>
        {
            Interlocked.Increment(ref events.ValidatedInputs);
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            return !string.IsNullOrWhiteSpace(name);
        }).WithMessage("Name is required.");
    }
}
public sealed class ErrorProbeHandler(ApiErrorHandlingTests.ProbeEvents events) : IRequestHandler<ErrorProbeCommand, string>
{
    public Task<string> Handle(ErrorProbeCommand request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref events.HandledInputs);
        return Task.FromResult(request.Name!);
    }
}

[ApiController]
[Route("error-probe")]
public sealed class ErrorProbeController(IMediator mediator) : ControllerBase
{
    [HttpPost("binding")]
    public async Task<IActionResult> Binding(ErrorProbeInput input, CancellationToken cancellationToken) =>
        ApiResponseFactory.Success(await mediator.Send(new ErrorProbeCommand(input.Name), cancellationToken)).ToActionResult();

    [HttpGet("secure"), Authorize(Roles = "Admin")]
    public IActionResult Secure() => NoContent();
    [HttpGet("limited"), EnableRateLimiting("probe")]
    public IActionResult Limited() => NoContent();
    [HttpGet("empty")]
    public IActionResult EmptyResponse() => NoContent();
    [HttpGet("file")]
    public IActionResult Image() => File(new MemoryStream([137, 80, 78, 71]), "image/png");
    [HttpGet("value-failure")]
    public IActionResult ValueFailure() => ApiResponseFactory.Error("PLATE_EXISTS", "A vehicle with this plate is already registered.", 409).ToActionResult();
    [HttpGet("conflict")]
    public IActionResult ConflictFailure() => throw new ProfileAccessConflictException(new InvalidOperationException("secret"));
    [HttpGet("unexpected/{id}")]
    public IActionResult Unexpected() => throw new DbUpdateException("secret SQL password name", new Exception("secret-inner"));
}
