using System.Security.Cryptography;
using System.Text;
using Npgsql;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartParking.ReservationService.Persistence;
using SmartParking.ReservationService.Application;
using SmartParking.ReservationService.Domain;

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
        builder.Services.AddScoped<StructureSafetyService>();
        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(context.Request.Headers["X-Service-Key"].ToString()), Encoding.UTF8.GetBytes(key)))
            { context.Response.StatusCode = 401; return; }
            try { await next(context); }
            catch (InvalidStructureEditException error)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { code = "INVALID_STRUCTURE", message = error.Message });
            }
            catch (HoldConflictException) { context.Response.StatusCode = 409; await context.Response.WriteAsJsonAsync(new { code = "STRUCTURE_BUSY" }); }
        });

        app.MapPut("/internal/structure/{siteId:guid}/{token:guid}",
            async (Guid siteId, Guid token, HoldRequest body, StructureSafetyService service, CancellationToken ct) =>
                Results.Ok(await service.PrepareAsync(body.TenantId, siteId, token, ct)));

        app.MapPost("/internal/structure/{siteId:guid}/impact",
            async (Guid siteId, HoldRequest body, StructureSafetyService service, CancellationToken ct) =>
                Results.Ok(await service.ImpactAsync(body.TenantId, siteId, ct)));

        app.MapPost("/internal/structure/{siteId:guid}/{token:guid}/release",
            async (Guid siteId, Guid token, ReleaseRequest body, StructureSafetyService service, CancellationToken ct) =>
            {
                await service.CompleteAsync(body.TenantId, siteId, token, body.Outcome, ct);
                return Results.NoContent();
            });
        return app;
    }
}
public sealed record HoldRequest(Guid TenantId);
public sealed record ReleaseRequest(Guid TenantId, EditOutcome? Outcome);
public static class Program { public static void Main(string[] args) => ReservationHost.Build(args).Run(); }
