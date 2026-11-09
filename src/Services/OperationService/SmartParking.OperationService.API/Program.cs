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
        }
        builder.Services.AddReservationMessaging(builder.Configuration);
        builder.Services.AddExceptionHandler<ReservationExceptionHandler>();
        builder.Services.Configure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = context =>
            new BadRequestObjectResult(new { success = false, code = "INVALID_REQUEST", message = "The request body or parameters are invalid.", traceId = context.HttpContext.TraceIdentifier }));
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
        app.UseExceptionHandler(new ExceptionHandlerOptions
        {
            SuppressDiagnosticsCallback = context => context.Exception is not OperationCanceledException
        });
        app.Use(async (context, next) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (!admissionEnabled && context.Request.Path.StartsWithSegments("/api/reservation-requests"))
            {
                context.Response.StatusCode = 503;
                if (!HttpMethods.IsHead(context.Request.Method))
                    await context.Response.WriteAsJsonAsync(new { success = false, code = "RESERVATION_ADMISSION_DISABLED", message = "Reservation admission is not enabled.", traceId = context.TraceIdentifier });
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
