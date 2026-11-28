using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using SmartParking.PaymentService.API.Authentication;
using SmartParking.PaymentService.API.Extensions;
using SmartParking.PaymentService.Application.Common.Behaviors;

namespace SmartParking.PaymentService.Tests.API.Probes;

internal sealed class PaymentErrorTestHost(
    WebApplication app,
    PaymentErrorProbeEvents events,
    PaymentErrorCapturedLogs logs,
    RSA s2sRsa) : IAsyncDisposable
{
    private static readonly SymmetricSecurityKey UserKey =
        new(Encoding.UTF8.GetBytes("isolated-payment-http-test-signing-key-32-bytes"));

    public HttpClient Client { get; } = app.GetTestClient();
    public PaymentErrorProbeEvents Events { get; } = events;
    public PaymentErrorCapturedLogs Logs { get; } = logs;

    public string CreateUserToken(string role = "DRIVER") =>
        new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: "payment-error-test",
            audience: "payment-error-test",
            claims: [new Claim("role", role)],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(UserKey, SecurityAlgorithms.HmacSha256)));

    public string CreateOperationsToken(bool includePermission = true)
    {
        var claims = new List<Claim>
        {
            new("sub", "SmartParking.OperationService"),
            new("sender", "SmartParking.OperationService"),
            new("role", "INTERNAL_SERVICE"),
            new("jti", Guid.NewGuid().ToString("N"))
        };
        if (includePermission)
        {
            claims.Add(new Claim("permission", OperationsServiceAuthentication.Permission));
        }

        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: "SmartParking.OperationService",
            audience: "SmartParking.PaymentService",
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(1),
            signingCredentials: new SigningCredentials(new RsaSecurityKey(s2sRsa), SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static async Task<PaymentErrorTestHost> StartAsync(
        bool completeAuthenticationResponses = false,
        string environment = "Testing")
    {
        var s2sRsa = RSA.Create(2048);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ReservationIntegration:Enabled"] = "true",
            ["ReservationIntegration:OperationsPublicKeyPem"] = s2sRsa.ExportSubjectPublicKeyInfoPem()
        });

        var logs = new PaymentErrorCapturedLogs();
        builder.Logging.ClearProviders().AddProvider(logs)
            .SetMinimumLevel(LogLevel.Information)
            .AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

        var events = new PaymentErrorProbeEvents();
        builder.Services.AddSingleton(events);
        builder.Services.AddControllers().AddApplicationPart(typeof(PaymentErrorProbeController).Assembly);
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = "payment-error-test",
                    ValidateAudience = true,
                    ValidAudience = "payment-error-test",
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = UserKey,
                    ValidateLifetime = true,
                    RoleClaimType = "role"
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = _ =>
                    {
                        Interlocked.Increment(ref events.Validated);
                        return Task.CompletedTask;
                    },
                    OnChallenge = async context =>
                    {
                        Interlocked.Increment(ref events.Challenges);
                        if (completeAuthenticationResponses)
                        {
                            context.HandleResponse();
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            await context.Response.WriteAsync("custom challenge");
                        }
                    },
                    OnForbidden = async context =>
                    {
                        Interlocked.Increment(ref events.Forbidden);
                        if (completeAuthenticationResponses)
                        {
                            context.Response.StatusCode = StatusCodes.Status403Forbidden;
                            await context.Response.WriteAsync("custom forbid");
                        }
                    }
                };
            });
        builder.Services.AddOperationsServiceAuthentication(builder.Configuration);
        builder.Services.AddPaymentApiErrorResponses();
        builder.Services.AddAuthorization();
        builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<PaymentErrorProbeCommand>());
        builder.Services.AddTransient<IValidator<PaymentErrorProbeCommand>, PaymentErrorProbeValidator>();
        builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddFixedWindowLimiter("probe", limiter =>
            {
                limiter.PermitLimit = 1;
                limiter.Window = TimeSpan.FromHours(1);
                limiter.QueueLimit = 0;
            });
        });

        var app = builder.Build();
        app.UseRouting();
        app.UsePaymentApiErrorResponses();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
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
        return new PaymentErrorTestHost(app, events, logs, s2sRsa);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await app.DisposeAsync();
        s2sRsa.Dispose();
    }
}
