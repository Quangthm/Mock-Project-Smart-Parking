using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartParking.ParkingService.Application;
using SmartParking.ParkingService.Application.Common.Interfaces.Persistence;
using SmartParking.ParkingService.Domain;
using SmartParking.ParkingService.Persistence;
using SmartParking.ParkingService.Persistence.Repositories;
using SmartParking.ParkingService.Infrastructure;

namespace SmartParking.ParkingService.API;

public static class ParkingHost
{
    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        configure?.Invoke(builder);
        var key = builder.Configuration["Services:Key"];
        if (string.IsNullOrWhiteSpace(key) || key.Length < 32) throw new InvalidOperationException("Configure Services:Key (at least 32 characters).");
        builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("Parking") ?? throw new InvalidOperationException("Configure ConnectionStrings:Parking.")));
        builder.Services.AddDbContext<ParkingDbContext>(options =>
            options.UseNpgsql(builder.Configuration.GetConnectionString("Parking")
                ?? throw new InvalidOperationException("Configure ConnectionStrings:Parking.")));
        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(allowIntegerValues: false)));
        builder.Services.AddHttpClient<OwnerIdentityClient>(c => { c.BaseAddress = new(builder.Configuration["Services:User"] ?? "http://localhost:5035/"); c.Timeout = TimeSpan.FromSeconds(10); });
        builder.Services.AddScoped<IOwnerAuthorizer>(s => s.GetRequiredService<OwnerIdentityClient>());
        builder.Services.AddHttpClient<SlotOperatorClient>(c => { c.BaseAddress = new(builder.Configuration["Services:User"] ?? "http://localhost:5035/"); c.Timeout = TimeSpan.FromSeconds(10); });
        builder.Services.AddScoped<ISlotOperatorAuthorizer>(s => s.GetRequiredService<SlotOperatorClient>());
        builder.Services.AddHttpClient<IStructureCommitments, StructureCommitmentsClient>(c =>
        { c.BaseAddress = new(builder.Configuration["Services:Reservation"] ?? "http://localhost:5055/"); c.Timeout = TimeSpan.FromSeconds(10); c.DefaultRequestHeaders.Add("X-Service-Key", key); });
        builder.Services.AddScoped<IParkingStructureRepository, PostgresParkingStructureRepository>();
        builder.Services.AddScoped<ParkingStructureService>();
        builder.Services.AddSingleton(_ => new ParkingGateConnectionPool(
            builder.Configuration.GetConnectionString("Parking")
                ?? throw new InvalidOperationException("Configure ConnectionStrings:Parking."),
            builder.Configuration.GetValue("Services:ParkingGateLimit", 16)));
        builder.Services.AddScoped<IStructureSynchronizationStore, PostgresStructureSynchronizationStore>();
        builder.Services.AddScoped<StructureSynchronizationService>();
        builder.Services.AddHostedService<StructureSynchronizationWorker>();
        builder.Services.AddScoped<IStructureOperationStore, StructureOperationStore>();
        builder.Services.AddScoped<StructureOperationService>();
        builder.Services.AddHttpClient<IStructureImpactPlanner, StructureImpactPlanner>(c => { c.BaseAddress = new Uri(builder.Configuration["Services:Reservation"] ?? "http://localhost:5055/"); c.Timeout = TimeSpan.FromSeconds(15); c.DefaultRequestHeaders.Add("X-Service-Key", key); });
        builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173", "http://localhost:8443", "http://127.0.0.1:8443").AllowAnyHeader().AllowAnyMethod()));
        builder.Services.AddControllers();
        builder.Services.AddHttpClient<OperatorSiteClient>(c => { c.BaseAddress = new(builder.Configuration["Services:User"] ?? "http://localhost:5035/"); c.Timeout = TimeSpan.FromSeconds(10); });
        builder.Services.AddScoped<IOperatorSiteAuthorizer>(s => s.GetRequiredService<OperatorSiteClient>());
        builder.Services.AddScoped<OperatorLayoutService>();
        builder.Services.AddScoped<OperatorOperationsStore>();
        builder.Services.AddScoped<OperatorGateSlotStore>();
        var app = builder.Build();
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
        app.UseCors();
        app.Use(async (context, next) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (context.Request.Path.StartsWithSegments("/internal") && !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(context.Request.Headers["X-Service-Key"].ToString()), Encoding.UTF8.GetBytes(key)))
            { context.Response.StatusCode = 401; return; }
            try { await next(context); }
            catch (PostgresException error) when (
                context.Request.Path.StartsWithSegments("/api/operator") &&
                (error.SqlState is PostgresErrorCodes.UndefinedTable or PostgresErrorCodes.UndefinedColumn))
            {
                context.Response.StatusCode = 503;
                await context.Response.WriteAsJsonAsync(new { success = false, code = "PROTOTYPE_SCHEMA_NOT_READY", message = "Operator data is unavailable until database reconciliation is complete." });
            }
            catch (StructureException e)
            {
                context.Response.StatusCode = e.Code switch
                {
                    "UNAUTHENTICATED" => 401,
                    "FORBIDDEN" => 403,
                    "SITE_NOT_FOUND" or "UNIT_NOT_FOUND" or "SLOT_NOT_FOUND" => 404,
                    "IDENTITY_UNAVAILABLE" or "COMMITMENTS_UNAVAILABLE" => 503,
                    "STRUCTURE_BUSY" or "STRUCTURE_IN_USE" or "STRUCTURE_CONFLICT" or "DUPLICATE_IDENTIFIER" or "NO_AVAILABLE_SLOT" => 409,
                    _ => 400
                };
                await context.Response.WriteAsJsonAsync(new { success = false, code = e.Code, message = e.Message });
            }
        });
        app.MapPut("/internal/tenants/{id:guid}", async (Guid id, TenantBody body, NpgsqlDataSource source, CancellationToken ct) =>
        {
            if (id == Guid.Empty || string.IsNullOrWhiteSpace(body.Name) || body.Name.Length > 255 || string.IsNullOrWhiteSpace(body.Email) || body.Email.Length > 255 || string.IsNullOrWhiteSpace(body.Phone) || body.Phone.Length > 20) return Results.BadRequest();
            await using var db = await source.OpenConnectionAsync(ct);
            await using var cmd = new NpgsqlCommand("INSERT INTO tenants(id,code,name,contact_email,contact_phone) VALUES ($1,$2,$3,$4,$5) ON CONFLICT(id) DO NOTHING", db);
            cmd.Parameters.AddWithValue(id); cmd.Parameters.AddWithValue("T-" + id.ToString("N")); cmd.Parameters.AddWithValue(body.Name); cmd.Parameters.AddWithValue(body.Email); cmd.Parameters.AddWithValue(body.Phone);
            await cmd.ExecuteNonQueryAsync(ct);
            await using var check = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM tenants WHERE id=$1 AND name=$2 AND contact_email=$3 AND contact_phone=$4 AND status='ACTIVE' AND deleted_at IS NULL)", db);
            check.Parameters.AddWithValue(id); check.Parameters.AddWithValue(body.Name); check.Parameters.AddWithValue(body.Email); check.Parameters.AddWithValue(body.Phone);
            return (bool)(await check.ExecuteScalarAsync(ct))! ? Results.NoContent() : Results.Conflict();
        });
        app.MapPost("/internal/sites/validate", async (SiteValidation body, NpgsqlDataSource source, CancellationToken ct) =>
        {
            if (body.SiteIds is null || body.TenantIds is null || body.SiteIds.Length > 100 || body.TenantIds.Length > 100) return Results.BadRequest();
            await using var db = await source.OpenConnectionAsync(ct);
            await using var cmd = new NpgsqlCommand("SELECT s.id,s.tenant_id FROM parking_sites s JOIN tenants t ON t.id=s.tenant_id WHERE s.id=ANY($1) AND s.tenant_id=ANY($2) AND s.status='ACTIVE' AND s.deleted_at IS NULL AND t.status='ACTIVE' AND t.deleted_at IS NULL ORDER BY s.id", db);
            cmd.Parameters.AddWithValue(body.SiteIds); cmd.Parameters.AddWithValue(body.TenantIds);
            var sites = new List<object>(); await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) sites.Add(new { id = reader.GetGuid(0), tenantId = reader.GetGuid(1) });
            return Results.Ok(sites);
        });
        app.MapPost("/internal/gate-sites/{siteId:guid}/slots/claim", async (
            Guid siteId,
            GateSlotClaim body,
            OperatorGateSlotStore store,
            CancellationToken ct) =>
        {
            if (body.TenantId == Guid.Empty || siteId == Guid.Empty ||
                body.VehicleType is not ("CAR" or "MOTORCYCLE" or "ANY"))
                return Results.BadRequest();
            return Results.Ok(await store.ClaimAvailableAsync(
                body.TenantId,
                siteId,
                body.VehicleType,
                body.PreferredSlotId,
                body.TargetUnitId,
                body.AllowReservedSlot,
                ct));
        });
        app.MapPost("/internal/gate-sites/{siteId:guid}/slots/{slotId:guid}/release", async (
            Guid siteId,
            Guid slotId,
            GateSlotRelease body,
            OperatorGateSlotStore store,
            CancellationToken ct) =>
        {
            if (body.TenantId == Guid.Empty || siteId == Guid.Empty || slotId == Guid.Empty)
                return Results.BadRequest();
            await store.ReleaseAsync(body.TenantId, siteId, slotId, ct);
            return Results.NoContent();
        });
        app.MapControllers();
        app.MapOperatorOperations();
        return app;
    }
}
public sealed record GateSlotClaim(Guid TenantId, string VehicleType, Guid? PreferredSlotId = null, Guid? TargetUnitId = null, bool AllowReservedSlot = false);
public sealed record GateSlotRelease(Guid TenantId);
public sealed record TenantBody(string Name, string Email, string Phone);
[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record BackupMarkBody(string Reason);
public sealed record OperationBody(Guid IdempotencyKey, StructureEdit Edit, Guid? TenantId = null);
public sealed record SiteValidation(Guid[] SiteIds, Guid[] TenantIds);
public sealed record CreateSite(string Code, string Name, string Address, decimal? Latitude, decimal? Longitude, Guid? TenantId = null);
public sealed record UnitBody(UnitType Type, string Name, Guid? ParentId, int Capacity, Guid? TenantId = null);
public sealed record SlotBody(Guid UnitId, string Code, VehicleType VehicleType, SlotType Type = SlotType.STANDARD, Guid? TenantId = null);
public sealed record PathBody(string Code, Guid? From, Guid? To, string? MapData, Guid? TenantId = null);
public sealed record EditStructure(string Action, Guid ResourceId, Guid UnitId, string? Code, string? Name, string? Address, bool Active, int Capacity, VehicleType VehicleType, SlotType SlotType, string? Coordinates3D, string? Features, decimal? Latitude, decimal? Longitude, Guid? TenantId = null);
public static class Program { public static void Main(string[] args) => ParkingHost.Build(args).Run(); }
