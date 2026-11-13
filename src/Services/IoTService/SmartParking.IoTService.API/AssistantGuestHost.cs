using System.Threading.RateLimiting;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartParking.IoTService.API.Http;
using SmartParking.IoTService.API.Management;
using SmartParking.IoTService.Application.Assistant;
using SmartParking.IoTService.Application.Assistant.Guest;
using SmartParking.IoTService.Infrastructure.Assistant.Guest;
namespace SmartParking.IoTService.API;

public static class AssistantGuestHost
{
    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        configure?.Invoke(builder);
        builder.Services.AddAssistantHttp();
        builder.Services.AddAssistantApplication(includeDemo: false);
        // Tests replace all ports with synthetic dependencies; normal hosting requires existing managed setup.
        if (!builder.Services.Any(s => s.ServiceType == typeof(Application.Assistant.Management.IAssistantManagementStore)))
            builder.AddManagement(publicGuest: true);
        else
            builder.Services.AddAssistantProviderTransport();
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.TryAddSingleton(new GuestOptions(builder.Configuration.GetValue<bool>("Assistant:Guest:ReleaseEnabled")));
        builder.Services.TryAddSingleton<IPublicKnowledge>(sp => new PublicKnowledgeFile(
            builder.Configuration["Assistant:Guest:KnowledgeDirectory"] ?? "", sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<IMeterFactory>()));
        builder.Services.TryAddSingleton<IGuestAnswerFactory, GuestAnswerFactory>();
        var chatAvailable = builder.Services.Any(s => s.ServiceType == typeof(Npgsql.NpgsqlDataSource) || s.ServiceType == typeof(Application.Assistant.Chat.IChatStore));
        if (chatAvailable) builder.AddChat();
        var origins = builder.Configuration.GetSection("Assistant:Guest:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Any(origin => !Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") ||
            uri.GetLeftPart(UriPartial.Authority) != origin || !string.IsNullOrEmpty(uri.UserInfo) || origin.Contains('*')))
            throw new InvalidOperationException("Guest CORS requires explicit HTTP origins.");
        builder.Services.AddCors(o => o.AddPolicy("Guest", p => { if (origins.Length > 0) p.WithOrigins(origins); p.WithMethods("GET", "POST").WithHeaders("Content-Type", "Authorization").WithExposedHeaders("X-Correlation-ID", "Retry-After", "Location"); }));
        builder.Services.AddRateLimiter(o =>
        {
            o.OnRejected = async (ctx, ct) =>
            {
                ctx.HttpContext.Response.Headers.RetryAfter = "60";
                await AssistantErrors.WriteAsync(ctx.HttpContext, AssistantErrors.ForStatus(429));
            };
            o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
                ctx.GetEndpoint()?.Metadata.GetMetadata<GuestMessageAdmission>() is not null
                    ? RateLimitPartition.GetConcurrencyLimiter("guest", _ => new() { PermitLimit = 2, QueueLimit = 0 })
                    : RateLimitPartition.GetNoLimiter("other"));
            // Trust socket address only. Proxy forwarding requires separately reviewed KnownProxies setup.
            o.AddPolicy("guest-message", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new() { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            o.AddPolicy("chat-read", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.User.FindFirst("account_id")?.Value ?? "anonymous", _ => new() { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            o.AddPolicy("guest-status", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new() { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
        var app = builder.Build();
        app.UseMiddleware<AssistantRequestMetrics>();
        app.UseAssistantHttp();
        app.UseRouting();
        app.UseCors("Guest");
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapGuest();
        app.MapManagement();
        if (chatAvailable) app.MapChat();
        app.MapGet("/health/live", () => Results.NoContent());
        return app;
    }
}
