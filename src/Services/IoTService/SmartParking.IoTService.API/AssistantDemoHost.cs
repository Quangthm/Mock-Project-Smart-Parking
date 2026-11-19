using System.Diagnostics;
using MediatR;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartParking.IoTService.API.Http;
using SmartParking.IoTService.Application.Assistant;
using SmartParking.IoTService.Application.Assistant.Demo;
using SmartParking.IoTService.Infrastructure.Assistant.Demo;
using SmartParking.IoTService.Application.Assistant.Knowledge;
using SmartParking.IoTService.Infrastructure.Assistant.Knowledge;
using System.Threading.RateLimiting;
using SmartParking.IoTService.API.Management;

namespace SmartParking.IoTService.API;

/// <summary>Isolated opt-in demo host with optional provider or authenticated managed configuration.</summary>
public static class AssistantDemoHost
{
    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        configure?.Invoke(builder);
        // Never silently fall back to mock when production configuration is absent or invalid.
        if (!builder.Environment.IsEnvironment("Demo") ||
            !builder.Configuration.GetValue<bool>("Assistant:Demo:Enabled"))
            throw new InvalidOperationException("This executable is demo-only. Set environment Demo and explicitly enable Assistant:Demo:Enabled. Production hosting is not configured.");

        var origins = builder.Configuration.GetSection("Assistant:Demo:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Any(origin => !Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") || uri.GetLeftPart(UriPartial.Authority) != origin ||
            !string.IsNullOrEmpty(uri.UserInfo) || origin.Contains('*')))
            throw new InvalidOperationException("Demo CORS requires explicit HTTP origins without paths or wildcards.");

        builder.Services.AddAssistantHttp();
        builder.Services.AddAssistantApplication();
        builder.Services.AddSingleton<IDemoAssistantStatusProvider, DemoAssistantStatusProvider>();
        builder.Services.TryAddSingleton<IDemoReplyGenerator, DemoReplyGenerator>();
        var mode = builder.Configuration["Assistant:Demo:Mode"] ?? "mock";
        if (mode is not ("mock" or "provider" or "managed"))
            throw new InvalidOperationException("Assistant demo mode must be mock, provider or managed.");
        var management = builder.Configuration.GetValue<bool>("Assistant:Management:Enabled");
        if (mode == "managed" && !management) throw new InvalidOperationException("Managed mode requires authenticated management configuration.");
        if (management)
        {
            if (mode != "managed") throw new InvalidOperationException("Management requires managed mode so saved settings control the chat.");
            builder.AddManagement();
        }
        if (mode == "provider")
        {
            var provider = builder.Configuration["Assistant:Demo:Provider"] ?? "";
            var options = new DemoGroundedOptions(
                builder.Configuration["Assistant:Demo:KnowledgeDirectory"] ?? "",
                provider, builder.Configuration["Assistant:Demo:Model"] ?? "",
                builder.Configuration["Assistant:Demo:ApiKey"] ?? "");
            builder.Services.TryAddSingleton<IDemoGroundedGenerator>(_ => new DemoGroundedGenerator(options));
        }
        // Local anonymous demo only: bound paid dispatches; this is not a production quota policy.
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.OnRejected = async (context, ct) =>
                await AssistantErrors.WriteAsync(context.HttpContext, AssistantErrors.ForStatus(429));
            options.AddPolicy("demo-provider", _ => RateLimitPartition.GetFixedWindowLimiter("demo", _ => new()
            {
                PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0,
                AutoReplenishment = true
            }));
        });
        builder.Services.AddCors(options => options.AddPolicy("AssistantDemo", policy =>
        {
            if (origins.Length > 0) policy.WithOrigins(origins);
            policy.WithMethods("GET", "POST").WithHeaders("Content-Type", "Authorization")
                .WithExposedHeaders("X-Correlation-ID", "X-Assistant-Mode");
        }));

        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers["X-Assistant-Mode"] = "demo";
                return Task.CompletedTask;
            });
            var start = Stopwatch.GetTimestamp();
            try { await next(context); }
            finally
            {
                // Route template only: never log URL/query/body/Authorization or account identifiers.
                app.Logger.LogInformation("Assistant demo request completed. Route: {Route}; Status: {Status}; Aborted: {Aborted}; DurationMs: {DurationMs}; TraceId: {TraceId}",
                    (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched",
                    context.Response.StatusCode, context.RequestAborted.IsCancellationRequested,
                    Stopwatch.GetElapsedTime(start).TotalMilliseconds, context.TraceIdentifier);
            }
        });
        app.UseAssistantHttp();
        app.UseRouting();
        app.UseCors("AssistantDemo");
        if (management) { app.UseAuthentication(); app.UseAuthorization(); app.MapManagement(); }
        app.UseRateLimiter();

        app.MapGet("/health/live", () => Results.NoContent());
        app.MapGet("/health/ready", (HttpContext context) => Results.Json(
            new SuccessEnvelope<object>(true, new { isDemo = true, readyFor = "demo-api-only", assistantEnabled = false }, context.TraceIdentifier)));
        app.MapGet("/api/demo/assistant/capabilities", async (HttpContext context, IMediator mediator, CancellationToken ct) =>
        {
            if (context.Request.QueryString.HasValue)
                return Results.BadRequest(AssistantErrors.Envelope(context, AssistantErrors.ForStatus(400)));
            var status = await mediator.Send(new GetDemoAssistantStatusQuery(), ct);
            return Results.Ok(new SuccessEnvelope<DemoAssistantStatus>(true, status, context.TraceIdentifier));
        });
        // Intentionally no MapControllers: authenticated production routes are not exposed by this demo host.
        var messages = app.MapPost("/api/demo/assistant/messages", DemoMessageEndpoint.SendAsync);
        if (mode != "mock") messages.RequireRateLimiting("demo-provider");
        return app;
    }
}
