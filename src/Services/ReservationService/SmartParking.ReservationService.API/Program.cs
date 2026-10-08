using System.Security.Cryptography;
using System.Text;
using Npgsql;
using SmartParking.ReservationService.Persistence;

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
        builder.Services.AddScoped<StructureSafetyStore>();
        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(context.Request.Headers["X-Service-Key"].ToString()), Encoding.UTF8.GetBytes(key)))
            { context.Response.StatusCode = 401; return; }
            try { await next(context); }
            catch (HoldConflictException) { context.Response.StatusCode = 409; await context.Response.WriteAsJsonAsync(new { code = "STRUCTURE_BUSY" }); }
        });
        app.MapPut("/internal/structure/{siteId:guid}/{token:guid}", async (Guid siteId, Guid token, HoldRequest body, StructureSafetyStore store, CancellationToken ct) =>
            body.TenantId == Guid.Empty || siteId == Guid.Empty || token == Guid.Empty ? Results.BadRequest() : Results.Ok(await store.AcquireAsync(body.TenantId, siteId, token, ct)));
        app.MapPost("/internal/structure/{siteId:guid}/impact",async(Guid siteId,HoldRequest body,StructureSafetyStore store,CancellationToken ct)=>body.TenantId==Guid.Empty || siteId==Guid.Empty?Results.BadRequest():Results.Ok(await store.ImpactAsync(body.TenantId,siteId,ct)));
        app.MapPost("/internal/structure/{siteId:guid}/{token:guid}/release", async (Guid siteId, Guid token, ReleaseRequest body, StructureSafetyStore store, CancellationToken ct) =>
        {
            if (body.TenantId == Guid.Empty || siteId == Guid.Empty || token == Guid.Empty ||
                body.Outcome is { RemovedSlots: null } or { RemovedUnits: null } or { Capacities: null } ||
                body.Outcome?.Capacities.Any(c => c.Capacity < 0 || c.Backup<0 || c.Backup>c.Capacity || c.VehicleType is not ("CAR" or "MOTORCYCLE")) == true) return Results.BadRequest();
            await store.ReleaseAsync(body.TenantId, siteId, token, body.Outcome, ct); return Results.NoContent();
        });
        return app;
    }
}
public sealed record HoldRequest(Guid TenantId);
public sealed record ReleaseRequest(Guid TenantId, EditOutcome? Outcome);
public static class Program { public static void Main(string[] args) => ReservationHost.Build(args).Run(); }
