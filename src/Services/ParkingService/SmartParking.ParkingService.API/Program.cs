using System.Security.Cryptography;
using System.Text;
using Npgsql;
using SmartParking.ParkingService.Application;
using SmartParking.ParkingService.Domain;
using SmartParking.ParkingService.Persistence;
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
        async Task<OwnerScope> Scope(OwnerIdentityClient identity, Guid? tenant, CancellationToken ct)
        {
            var scope = await identity.ScopeAsync(ct);
            var selected = tenant ?? (scope.TenantIds.Length == 1 ? scope.TenantIds[0] : Guid.Empty);
            if (!scope.TenantIds.Contains(selected)) throw new StructureException("FORBIDDEN", "Select an authorized tenant.");
            return new(scope.UserId, selected);
        }
        app.MapGet("/api/parking-lots", async (Guid? tenantId, OwnerIdentityClient identity, ParkingStructureService service, CancellationToken ct) => Results.Ok(new { success = true, data = await service.ListAsync(await Scope(identity, tenantId, ct), ct) }));
        app.MapPost("/api/parking-lots", async (CreateSite body, OwnerIdentityClient identity, ParkingStructureService service, CancellationToken ct) =>
        {
            var site = await service.CreateAsync(await Scope(identity, body.TenantId, ct), body.Code, body.Name, body.Address, body.Latitude, body.Longitude, ct);
            return Results.Created($"/api/parking-lots/{site.Id}/structure", new { success = true, data = site });
        });
        app.MapGet("/api/parking-lots/{id:guid}/structure", async (Guid id, Guid? tenantId, OwnerIdentityClient identity, ParkingStructureService service, CancellationToken ct) => Results.Ok(new { success = true, data = await service.GetAsync(await Scope(identity, tenantId, ct), id, ct) }));
        app.MapGet("/api/parking-lots/{id:guid}/operational-layout", async (Guid id, SlotOperatorClient identity, ParkingStructureService service, CancellationToken ct) => Results.Ok(new { success = true, data = await service.ReadOperationalAsync(await identity.ScopeAsync(id, ct), id, ct) }));
        app.MapPost("/api/parking-lots/{id:guid}/slots/{slot:guid}/backup", async (Guid id, Guid slot, BackupMarkBody body, SlotOperatorClient identity, ParkingStructureService service, CancellationToken ct) =>
        { await service.MarkOperationalBackupAsync(await identity.ScopeAsync(id, ct), id, slot, body.Reason, ct); return Results.Ok(new { success = true }); });
        app.MapPost("/api/parking-lots/{id:guid}/operations", async (Guid id, OperationBody body, OwnerIdentityClient identity, StructureOperationService operations, CancellationToken ct) =>
        {
            var result = await operations.ExecuteAsync(await Scope(identity, body.TenantId, ct), id, body.IdempotencyKey, body.Edit, ct);
            return Results.Json(new { success = result.Status == "completed", data = result, message = $"Operation {result.Id}: {result.Status}. {result.Reason}" }, statusCode: result.Status == "completed" ? 200 : result.Status == "pending" ? 202 : 409);
        });
        app.MapGet("/api/parking-lots/{id:guid}/operations/{operationId:guid}", async (Guid id, Guid operationId, Guid? tenantId, OwnerIdentityClient identity, StructureOperationService operations, CancellationToken ct) => Results.Ok(new { success = true, data = await operations.ReadAsync(await Scope(identity, tenantId, ct), id, operationId, ct) }));
        app.MapPost("/api/parking-lots/{id:guid}/units", async (Guid id, UnitBody body, OwnerIdentityClient identity, ParkingStructureService service, CancellationToken ct) => Results.Ok(new { success = true, data = await service.AddUnitAsync(await Scope(identity, body.TenantId, ct), id, body.Type, body.Name, body.ParentId, body.Capacity, ct) }));
        app.MapPost("/api/parking-lots/{id:guid}/slots", async (Guid id, SlotBody body, OwnerIdentityClient identity, ParkingStructureService service, CancellationToken ct) => Results.Ok(new { success = true, data = await service.AddSlotAsync(await Scope(identity, body.TenantId, ct), id, body.UnitId, body.Code, body.VehicleType, body.Type, ct) }));
        app.MapPatch("/api/parking-lots/{id:guid}/structure", async (Guid id, EditStructure body, OwnerIdentityClient identity, ParkingStructureService service, CancellationToken ct) =>
        {
            if (body.Action == "backup") await identity.ScopeAsync(ct, "BACKUP_CONFIGURE");
            if (body.Action == "markBackup") await identity.ScopeAsync(ct, "BACKUP_MARK");
            var edit = new StructureEdit(
                body.Action, body.ResourceId, body.UnitId, body.Code, body.Name, body.Address,
                body.Active, body.Capacity, body.VehicleType, body.SlotType,
                body.Coordinates3D, body.Features, body.Latitude, body.Longitude);
            await service.ApplyAsync(await Scope(identity, body.TenantId, ct), id, edit, ct);
            return Results.Ok(new { success = true });
        });
        app.MapPost("/api/parking-lots/{id:guid}/paths", async (Guid id, PathBody body, OwnerIdentityClient identity, ParkingStructureService service, CancellationToken ct) => Results.Ok(new { success = true, data = await service.AddAccessPathAsync(await Scope(identity, body.TenantId, ct), id, body.Code, body.From, body.To, body.MapData, ct) }));
        app.MapGet("/api/parking-lots/{id:guid}/operator-layout", async (Guid id, OperatorLayoutService service, CancellationToken ct) =>
            Results.Ok(new { success = true, data = await service.ReadAsync(id, ct) }));
        app.MapDelete("/api/parking-lots/{id:guid}/slots/{slot:guid}/backup", async (Guid id, Guid slot, SlotOperatorClient identity, ParkingStructureService service, CancellationToken ct) =>
        {
            await service.SetOperationalBackupAsync(await identity.ScopeAsync(id, ct), id, slot, false, "Backup designation released by operator.", ct);
            return Results.Ok(new { success = true });
        });
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
