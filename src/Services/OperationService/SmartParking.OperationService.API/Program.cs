using SmartParking.OperationService.Application;
using SmartParking.OperationService.API.Controllers;
using SmartParking.OperationService.API.Middleware;
using SmartParking.OperationService.Infrastructure;

namespace SmartParking.OperationService.API;

public static class ReservationHost
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
        builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
            .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173", "http://localhost:8443", "http://127.0.0.1:8443")
            .AllowAnyHeader()
            .AllowAnyMethod()));
        var app = builder.Build();
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
        app.UseCors();
        app.UseMiddleware<ReservationExceptionMiddleware>();
        app.UseMiddleware<InternalServiceKeyMiddleware>();
        app.MapControllers();
        return app;
    }
}
public static class Program { public static void Main(string[] args) => ReservationHost.Build(args).Run(); }
