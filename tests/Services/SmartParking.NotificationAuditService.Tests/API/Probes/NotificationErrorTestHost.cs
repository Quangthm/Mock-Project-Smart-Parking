using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using SmartParking.NotificationAuditService.API.Extensions;
using SmartParking.NotificationAuditService.Application.Common.Behaviors;

namespace SmartParking.NotificationAuditService.Tests.API.Probes;

internal sealed class NotificationErrorTestHost(
    WebApplication app,
    NotificationErrorProbeEvents events,
    NotificationErrorCapturedLogs logs) : IAsyncDisposable
{
    private static readonly SymmetricSecurityKey UserKey =
        new(Encoding.UTF8.GetBytes("isolated-notification-http-test-signing-key-32-bytes"));

    public HttpClient Client { get; } = app.GetTestClient();
    public NotificationErrorProbeEvents Events { get; } = events;
    public NotificationErrorCapturedLogs Logs { get; } = logs;

    public string CreateUserToken(string role = "DRIVER") =>
        new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: "notification-error-test",
            audience: "notification-error-test",
            claims: [new Claim("role", role)],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(UserKey, SecurityAlgorithms.HmacSha256)));

    public static async Task<NotificationErrorTestHost> StartAsync(
        bool completeAuthenticationResponses = false,
        string environment = "Testing")
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();

        var logs = new NotificationErrorCapturedLogs();
        builder.Logging.ClearProviders().AddProvider(logs)
            .SetMinimumLevel(LogLevel.Information)
            .AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

        var events = new NotificationErrorProbeEvents();
        builder.Services.AddSingleton(events);
        builder.Services.AddControllers().AddApplicationPart(typeof(NotificationErrorProbeController).Assembly);
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = "notification-error-test",
                    ValidateAudience = true,
                    ValidAudience = "notification-error-test",
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
        builder.Services.AddNotificationAuditApiErrorResponses();
        builder.Services.AddAuthorization();
        builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<NotificationErrorProbeCommand>());
        builder.Services.AddTransient<IValidator<NotificationErrorProbeCommand>, NotificationErrorProbeValidator>();
        builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
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
        app.UseNotificationAuditApiErrorResponses();
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
        return new NotificationErrorTestHost(app, events, logs);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await app.DisposeAsync();
    }
}
