using System.Security.Cryptography;
using System.Text;
using Npgsql;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartParking.ReservationService.Persistence;
using SmartParking.ReservationService.Application;
using SmartParking.ReservationService.Domain;
using SmartParking.ReservationService.API.Controllers;

namespace SmartParking.ReservationService.API;

public static class ReservationHost
{
    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        configure?.Invoke(builder);
        var key = builder.Configuration["Services:Key"];
        if (string.IsNullOrWhiteSpace(key) || key.Length < 32) throw new InvalidOperationException("Configure Services:Key (at least 32 characters).");
        builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("Reservation") ?? throw new InvalidOperationException("Configure ConnectionStrings:Reservation.")));
        builder.Services.TryAddScoped<IStructureSafetyStore, StructureSafetyStore>();
        builder.Services.AddScoped<IReservationGateStore, ReservationGateStore>();
        builder.Services.AddScoped<IOperatorIdentityClient>(services =>
            services.GetRequiredService<OperatorIdentityClient>());
        builder.Services.AddApplicationServices();
        builder.Services.AddControllers().AddApplicationPart(typeof(ReservationsController).Assembly);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddHttpClient<OperatorIdentityClient>(client =>
        {
            client.BaseAddress = new(builder.Configuration["Services:User"] ?? "http://localhost:5035/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
            .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173", "http://localhost:8443", "http://127.0.0.1:8443")
            .AllowAnyHeader()
            .AllowAnyMethod()));
        var app = builder.Build();
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
        app.UseCors();
        app.Use(async (context, next) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (context.Request.Path.StartsWithSegments("/internal") && !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(context.Request.Headers["X-Service-Key"].ToString()), Encoding.UTF8.GetBytes(key)))
            { context.Response.StatusCode = 401; return; }
            try { await next(context); }
            catch (PostgresException error) when (
                (context.Request.Path.StartsWithSegments("/api/reservations") || context.Request.Path.StartsWithSegments("/api/parking-sessions")) &&
                (error.SqlState is PostgresErrorCodes.UndefinedTable or PostgresErrorCodes.UndefinedColumn))
            {
                context.Response.StatusCode = 503;
                await context.Response.WriteAsJsonAsync(new { success = false, code = "PROTOTYPE_SCHEMA_NOT_READY", message = "Reservation data is unavailable until database reconciliation is complete." });
            }
            catch (InvalidStructureEditException error)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { code = "INVALID_STRUCTURE", message = error.Message });
            }
            catch (ReservationGateException error)
            {
                context.Response.StatusCode = error.StatusCode;
                await context.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    code = error.Code,
                    message = error.Message
                });
            }
            catch (HoldConflictException) { context.Response.StatusCode = 409; await context.Response.WriteAsJsonAsync(new { code = "STRUCTURE_BUSY" }); }
        });

        app.MapControllers();
        return app;
    }
}
public static class Program { public static void Main(string[] args) => ReservationHost.Build(args).Run(); }
