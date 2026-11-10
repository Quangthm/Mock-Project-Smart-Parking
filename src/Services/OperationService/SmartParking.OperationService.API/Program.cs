using Microsoft.AspNetCore.SignalR;
using SmartParking.OperationService.Application;
using SmartParking.OperationService.API.Controllers;
using SmartParking.OperationService.API.Hubs;
using SmartParking.OperationService.API.Middleware;
using SmartParking.OperationService.API.Realtime;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Infrastructure;

namespace SmartParking.OperationService.API;

public static class OperationHost
{
    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        configure?.Invoke(builder);
        var key = builder.Configuration["Services:Key"];
        if (string.IsNullOrWhiteSpace(key) || key.Length < 32) throw new InvalidOperationException("Configure Services:Key (at least 32 characters).");
        builder.Services.AddApplicationServices();
        builder.Services.AddInfrastructureServices(builder.Configuration);
        builder.Services.AddControllers().AddApplicationPart(typeof(ReservationsController).Assembly);

        // SPARK-244 P2: realtime operator monitoring hub. Redis backplane is
        // enabled only when a connection string is configured (multi-instance).
        var signalR = builder.Services.AddSignalR();
        var redis = builder.Configuration["Redis:ConnectionString"]
            ?? builder.Configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redis))
        {
            var normalizedRedis = redis.StartsWith("redis://", StringComparison.OrdinalIgnoreCase)
                ? redis["redis://".Length..]
                : redis;
            signalR.AddStackExchangeRedis(normalizedRedis, options =>
            {
                options.Configuration.AbortOnConnectFail = false;
            });
        }
        builder.Services.AddSingleton<IRealtimeLayoutBroadcaster, SignalRLayoutRealtimeBroadcaster>();

        var configuredOrigins = builder.Configuration["Cors:AllowedOrigins"];
        var allowedOrigins = !string.IsNullOrWhiteSpace(configuredOrigins)
            ? configuredOrigins.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            : builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()));
        var app = builder.Build();
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
        app.UseCors();
        app.UseMiddleware<ReservationExceptionMiddleware>();
        app.UseMiddleware<InternalServiceKeyMiddleware>();
        app.MapControllers();
        app.MapHub<ParkingLotLayoutHub>("/ws/parking-lots/{siteId}/layout");
        return app;
    }
}
public static class Program { public static void Main(string[] args) => OperationHost.Build(args).Run(); }
