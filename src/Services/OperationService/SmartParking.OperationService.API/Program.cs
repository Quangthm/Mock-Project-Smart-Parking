using Microsoft.AspNetCore.SignalR;
using SmartParking.OperationService.Application;
using SmartParking.OperationService.API.Controllers;
using SmartParking.OperationService.API.Hubs;
using SmartParking.OperationService.API.Middleware;
using SmartParking.OperationService.API.Realtime;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Infrastructure;
using SmartParking.OperationService.Infrastructure.Authentication;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Infrastructure.Reservations;
using Microsoft.AspNetCore.Mvc;

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
        builder.AddParkingSearch();
        var admissionEnabled = builder.Configuration.GetValue<bool>("ReservationAdmission:Enabled");
        builder.Services.AddAuthorization();
        builder.Services.AddScoped<ICurrentActor, HttpCurrentActor>();
        if (admissionEnabled)
        {
            builder.Services.AddConsumerAuthentication(builder.Configuration);
            builder.Services.PostConfigure<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>(
                Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.Events.OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.Headers.WWWAuthenticate = "Bearer";
                        var unavailable = ConsumerAuthenticationRegistration.KeysUnavailable(context.HttpContext);
                        await ApiErrors.WriteAsync(context.HttpContext, unavailable ? 503 : 401,
                            unavailable ? "AUTHENTICATION_KEYS_UNAVAILABLE" : "UNAUTHENTICATED",
                            unavailable ? "Authentication is temporarily unavailable." : "Authentication is required.",
                            ct: context.HttpContext.RequestAborted);
                    };
                    options.Events.OnForbidden = async context =>
                    {
                        await ApiErrors.WriteAsync(context.HttpContext, 403, "FORBIDDEN", "Access is denied.",
                            ct: context.HttpContext.RequestAborted);
                    };
                });
        }
        builder.Services.AddReservationMessaging(builder.Configuration);
        builder.Services.AddExceptionHandler<ReservationExceptionHandler>();
        ApiErrors.Configure(builder.Services);
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

        var rawOrigins = builder.Configuration["Cors:AllowedOrigins"];
        var origins = !string.IsNullOrWhiteSpace(rawOrigins)
            ? rawOrigins.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173", "http://127.0.0.1:5173", "http://localhost:8443", "http://127.0.0.1:8443"];
        builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
        {
            if (origins.Length > 0) policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
        }));
        var app = builder.Build();
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
        app.UseCors();
        ApiErrors.Use(app);
        app.Use(async (context, next) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (!admissionEnabled && (context.Request.Path.StartsWithSegments("/api/reservation-requests") ||
                context.Request.Path.StartsWithSegments("/api/reservation-price-previews") ||
                context.Request.Path.StartsWithSegments("/api/reservation-terms") ||
                context.Request.Path.StartsWithSegments("/api/me/reservations")))
            {
                await ApiErrors.WriteAsync(context, 503, "RESERVATION_ADMISSION_DISABLED",
                    "Reservation admission is not enabled.", ct: context.RequestAborted);
                return;
            }
            await next(context);
        });
        app.UseMiddleware<InternalServiceKeyMiddleware>();
        if (admissionEnabled) app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapParkingSearch();
        app.MapHub<ParkingLotLayoutHub>("/ws/parking-lots/{siteId}/layout");
        return app;
    }
}
public static class Program { public static void Main(string[] args) => OperationHost.Build(args).Run(); }
