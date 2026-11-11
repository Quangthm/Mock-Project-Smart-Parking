using SmartParking.OperationService.Application;
using SmartParking.OperationService.API.Controllers;
using SmartParking.OperationService.API.Middleware;
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
        var origins = builder.Configuration["Cors:AllowedOrigins"]?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
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
        return app;
    }
}
public static class Program { public static void Main(string[] args) => OperationHost.Build(args).Run(); }
