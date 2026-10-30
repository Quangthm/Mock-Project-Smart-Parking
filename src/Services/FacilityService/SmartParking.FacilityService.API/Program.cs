using System.Security.Cryptography;
using System.Text;
using Npgsql;
using SmartParking.FacilityService.Application;
using SmartParking.FacilityService.Domain;
using SmartParking.FacilityService.Persistence;
using SmartParking.FacilityService.Infrastructure;

namespace SmartParking.FacilityService.API;

public static class FacilityHost
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
        builder.Services.AddScoped<IOwnerScopeReader>(s => s.GetRequiredService<OwnerIdentityClient>());
        builder.Services.AddScoped<IOwnerAuthorizer>(s => s.GetRequiredService<OwnerIdentityClient>());
        builder.Services.AddHttpClient<SlotOperatorClient>(c => { c.BaseAddress = new(builder.Configuration["Services:User"] ?? "http://localhost:5035/"); c.Timeout = TimeSpan.FromSeconds(10); });
        builder.Services.AddScoped<ISlotOperatorAuthorizer>(s => s.GetRequiredService<SlotOperatorClient>());
        builder.Services.AddHttpClient<LayoutCallerResolver>(c => { c.BaseAddress = new(builder.Configuration["Services:User"] ?? "http://localhost:5035/"); c.Timeout = TimeSpan.FromSeconds(10); });
        builder.Services.AddScoped<ILayoutCallerResolver>(s => s.GetRequiredService<LayoutCallerResolver>());
        builder.Services.AddHttpClient<IStructureCommitments, StructureCommitmentsClient>(c =>
        { c.BaseAddress = new(builder.Configuration["Services:Reservation"] ?? "http://localhost:5055/"); c.Timeout = TimeSpan.FromSeconds(10); c.DefaultRequestHeaders.Add("X-Service-Key", key); });
        builder.Services.AddScoped<IParkingStructureRepository, PostgresParkingStructureRepository>();
        builder.Services.AddScoped<IOperationalLayoutReader, PostgresOperationalLayoutReader>();
        builder.Services.AddScoped<ParkingStructureService>();
        builder.Services.AddScoped<IOwnerOnboardingStore, OwnerOnboardingStore>();
        builder.Services.AddScoped<OwnerOnboardingService>();
        builder.Services.AddScoped<IFacilityDirectoryStore, FacilityDirectoryStore>();
        builder.Services.AddScoped<FacilityDirectoryService>();
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
        builder.Services.AddScoped<IOperatorOperationsStore, OperatorOperationsStore>();
        builder.Services.AddScoped<OperatorOperationsService>();
        builder.Services.AddScoped<IOperatorGateSlotStore, OperatorGateSlotStore>();
        builder.Services.AddScoped<OperatorGateSlotService>();
        builder.Services.AddScoped<IMaintenanceWindowStore, PostgresMaintenanceWindowStore>();
        builder.Services.AddScoped<MaintenanceWindowService>();
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
            catch (ReleasedIdentifierException e)
            {
                context.Response.StatusCode = 409;
                await context.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    code = e.Code,
                    message = e.Message,
                    released = new { id = e.ResourceId, code = e.ReleasedCode, deletedAt = e.DeletedAt }
                });
            }
            catch (StructureBatchException e)
            {
                context.Response.StatusCode = 409;
                await context.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    code = e.Code,
                    message = e.Message,
                    items = e.Failures.Select(f => new { resourceId = f.ResourceId, code = f.Code, message = f.Message })
                });
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
        app.MapPut("/internal/owner-onboarding/{id:guid}", async (Guid id, OwnerOnboardingBody body, OwnerOnboardingService store, CancellationToken ct) =>
        {
            await store.ProvisionAsync(id, body, ct);
            return Results.NoContent();
        });
        app.MapPut("/internal/tenants/{id:guid}", async (Guid id, TenantBody body, FacilityDirectoryService service, CancellationToken ct) =>
            await service.ProvisionTenantAsync(id, body, ct) ? Results.NoContent() : Results.Conflict());
        app.MapPost("/internal/sites/validate", async (SiteValidation body, FacilityDirectoryService service, CancellationToken ct) =>
            Results.Ok(await service.ValidateSitesAsync(body, ct)));
        app.MapPost("/internal/gate-sites/{siteId:guid}/slots/claim", async (
            Guid siteId,
            GateSlotClaim body,
            OperatorGateSlotService store,
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
            OperatorGateSlotService store,
            CancellationToken ct) =>
        {
            if (body.TenantId == Guid.Empty || siteId == Guid.Empty || slotId == Guid.Empty)
                return Results.BadRequest();
            await store.ReleaseAsync(body.TenantId, siteId, slotId, ct);
            return Results.NoContent();
        });
        async Task<OwnerScope> Scope(IOwnerScopeReader identity, Guid? tenant, CancellationToken ct)
        {
            var scope = await identity.ScopeAsync(ct);
            var selected = tenant ?? (scope.TenantIds.Length == 1 ? scope.TenantIds[0] : Guid.Empty);
            if (!scope.TenantIds.Contains(selected)) throw new StructureException("FORBIDDEN", "Select an authorized tenant.");
            return new(scope.UserId, selected);
        }
        app.MapGet("/api/parking-lots", async (Guid? tenantId, IOwnerScopeReader identity, ParkingStructureService service, CancellationToken ct) => Results.Ok(new { success = true, data = await service.ListAsync(await Scope(identity, tenantId, ct), ct) }));
        app.MapPost("/api/parking-lots", async (CreateSite body, IOwnerScopeReader identity, ParkingStructureService service, CancellationToken ct) =>
        {
            var site = await service.CreateAsync(await Scope(identity, body.TenantId, ct), body.Code, body.Name, body.Address, body.Latitude, body.Longitude, ct);
            return Results.Created($"/api/parking-lots/{site.Id}/structure", new { success = true, data = site });
        });
        app.MapGet("/api/parking-lots/{id:guid}/structure", async (Guid id, Guid? tenantId, IOwnerScopeReader identity, ParkingStructureService service, MaintenanceWindowService windows, CancellationToken ct) =>
        {
            var owner = await Scope(identity, tenantId, ct);
            var structure = await service.GetAsync(owner, id, ct);
            var serviceState = await windows.StateAsync(owner, id, structure.Site.Status, ct);
            return Results.Ok(new
            {
                success = true,
                data = new
                {
                    structure.Site,
                    structure.Units,
                    structure.Slots,
                    structure.Paths,
                    structure.BackupPolicies,
                    structure.CapacityViews,
                    serviceState = new
                    {
                        state = serviceState.State,
                        closedUntil = serviceState.ClosedUntil,
                        windows = serviceState.Windows.Select(window => new
                        {
                            window.Id,
                            window.Start,
                            window.End,
                            window.Reason,
                            window.EnforcementState
                        })
                    }
                }
            });
        });
        app.MapGet("/api/parking-lots/{id:guid}/operational-layout", async (
            Guid id,
            string? vehicleType,
            string? floor,
            HttpContext context,
            ParkingStructureService service,
            CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            context.Response.Headers.Vary = "Authorization";

            VehicleType? vehicleFilter = null;
            if (!string.IsNullOrWhiteSpace(vehicleType))
            {
                if (!Enum.TryParse<VehicleType>(vehicleType, ignoreCase: false, out var parsed)
                    || !Enum.IsDefined(parsed)
                    || !string.Equals(vehicleType, parsed.ToString(), StringComparison.Ordinal))
                    return Results.BadRequest(new { success = false, code = "INVALID_VEHICLE_TYPE", message = "vehicleType must be CAR, MOTORCYCLE, or OVERSIZED." });
                vehicleFilter = parsed;
            }

            try
            {
                var data = await service.ReadOperationalAsync(id, vehicleFilter, floor, ct);
                return Results.Ok(new { success = true, data });
            }
            catch (StructureException)
            {
                // Already mapped to 401/403/404/409/... by the outer middleware.
                throw;
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                // The client aborted; this is not a server failure.
                throw;
            }
            catch (Exception)
            {
                return Results.Json(
                    new { success = false, code = "INTERNAL_ERROR", message = "An unexpected error occurred." },
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        });
        app.MapPost("/api/parking-lots/{id:guid}/slots/{slot:guid}/backup", async (Guid id, Guid slot, BackupMarkBody body, ISlotOperatorAuthorizer identity, ParkingStructureService service, CancellationToken ct) =>
        { await service.MarkOperationalBackupAsync(await identity.ScopeAsync(id, ct), id, slot, body.Reason, ct); return Results.Ok(new { success = true }); });
        app.MapPost("/api/parking-lots/{id:guid}/operations", async (Guid id, OperationBody body, IOwnerScopeReader identity, StructureOperationService operations, CancellationToken ct) =>
        {
            var result = await operations.ExecuteAsync(await Scope(identity, body.TenantId, ct), id, body.IdempotencyKey, body.Edit, ct);
            return Results.Json(new { success = result.Status == "completed", data = result, message = $"Operation {result.Id}: {result.Status}. {result.Reason}" }, statusCode: result.Status == "completed" ? 200 : result.Status == "pending" ? 202 : 409);
        });
        app.MapGet("/api/parking-lots/{id:guid}/operations/{operationId:guid}", async (Guid id, Guid operationId, Guid? tenantId, IOwnerScopeReader identity, StructureOperationService operations, CancellationToken ct) => Results.Ok(new { success = true, data = await operations.ReadAsync(await Scope(identity, tenantId, ct), id, operationId, ct) }));
        app.MapPost("/api/parking-lots/{id:guid}/units", async (Guid id, UnitBody body, IOwnerScopeReader identity, ParkingStructureService service, CancellationToken ct) => Results.Ok(new { success = true, data = await service.AddUnitAsync(await Scope(identity, body.TenantId, ct), id, body.Type, body.Name, body.ParentId, body.Capacity, ct) }));
        app.MapPost("/api/parking-lots/{id:guid}/slots", async (Guid id, SlotBody body, IOwnerScopeReader identity, ParkingStructureService service, CancellationToken ct) => Results.Ok(new { success = true, data = await service.AddSlotAsync(await Scope(identity, body.TenantId, ct), id, body.UnitId, body.Code, body.VehicleType, body.Type, body.VehicleTypes, body.PrimaryVehicleType, ct) }));
        app.MapPatch("/api/parking-lots/{id:guid}/structure", async (Guid id, EditStructure body, IOwnerScopeReader identity, ParkingStructureService service, CancellationToken ct) =>
        {
            if (body.Action == "backup") await identity.ScopeAsync(ct, "BACKUP_CONFIGURE");
            if (body.Action == "markBackup") await identity.ScopeAsync(ct, "BACKUP_MARK");
            var edit = new StructureEdit(
                body.Action, body.ResourceId, body.UnitId, body.Code, body.Name, body.Address,
                body.Active, body.Capacity, body.VehicleType, body.SlotType,
                body.Coordinates3D, body.Features, body.Latitude, body.Longitude,
                body.VehicleTypes, body.PrimaryVehicleType);
            await service.ApplyAsync(await Scope(identity, body.TenantId, ct), id, edit, ct);
            return Results.Ok(new { success = true });
        });
        app.MapPost("/api/parking-lots/{id:guid}/paths", async (Guid id, PathBody body, IOwnerScopeReader identity, ParkingStructureService service, CancellationToken ct) => Results.Ok(new { success = true, data = await service.AddAccessPathAsync(await Scope(identity, body.TenantId, ct), id, body.Code, body.From, body.To, body.MapData, ct) }));
        app.MapGet("/api/parking-lots/{id:guid}/operator-layout", async (Guid id, ISlotOperatorAuthorizer identity, OperatorLayoutService service, CancellationToken ct) =>
        {
            // Legacy route serves the same aggregate; require the same SLOT_OVERRIDE gate as
            // /operational-layout so it cannot bypass that boundary. Payload is unchanged.
            await identity.ScopeAsync(id, ct);
            return Results.Ok(new { success = true, data = await service.ReadAsync(id, ct) });
        });
        app.MapDelete("/api/parking-lots/{id:guid}/slots/{slot:guid}/backup", async (Guid id, Guid slot, ISlotOperatorAuthorizer identity, ParkingStructureService service, CancellationToken ct) =>
        {
            await service.SetOperationalBackupAsync(await identity.ScopeAsync(id, ct), id, slot, false, "Backup designation released by operator.", ct);
            return Results.Ok(new { success = true });
        });
        app.MapGet("/api/parking-lots/{id:guid}/maintenance-windows", async (Guid id, Guid? tenantId, IOwnerScopeReader identity, MaintenanceWindowService windows, CancellationToken ct) =>
            Results.Ok(new { success = true, data = await windows.ListAsync(await Scope(identity, tenantId, ct), id, ct) }));
        app.MapPost("/api/parking-lots/{id:guid}/maintenance-windows", async (Guid id, MaintenanceWindowBody body, IOwnerScopeReader identity, MaintenanceWindowService windows, CancellationToken ct) =>
        {
            var window = await windows.CreateAsync(await Scope(identity, body.TenantId, ct), id, body.Start, body.End, body.Reason, null, null, ct);
            return Results.Created($"/api/parking-lots/{id}/maintenance-windows/{window.Id}", new { success = true, data = window });
        });
        app.MapPatch("/api/parking-lots/{id:guid}/maintenance-windows/{windowId:guid}", async (Guid id, Guid windowId, MaintenanceWindowPatch body, IOwnerScopeReader identity, MaintenanceWindowService windows, CancellationToken ct) =>
            Results.Ok(new { success = true, data = await windows.ExtendAsync(await Scope(identity, body.TenantId, ct), id, windowId, body.Start, body.End, ct) }));
        app.MapDelete("/api/parking-lots/{id:guid}/maintenance-windows/{windowId:guid}", async (Guid id, Guid windowId, Guid? tenantId, IOwnerScopeReader identity, MaintenanceWindowService windows, CancellationToken ct) =>
        {
            await windows.CancelAsync(await Scope(identity, tenantId, ct), id, windowId, ct);
            return Results.Ok(new { success = true });
        });
        app.MapPost("/api/parking-lots/{id:guid}/structure/batch", async (Guid id, BatchBody body, HttpContext context, IOwnerScopeReader identity, StructureOperationService operations, CancellationToken ct) =>
        {
            var header = context.Request.Headers["Idempotency-Key"].ToString();
            if (!Guid.TryParse(header, out var idempotencyKey)) return Results.BadRequest(new { success = false, code = "INVALID_STRUCTURE", message = "Idempotency-Key header is required." });
            if (body.IdempotencyKey is { } declared && declared != idempotencyKey) return Results.BadRequest(new { success = false, code = "INVALID_STRUCTURE", message = "Idempotency-Key header and body idempotencyKey must agree." });
            var edit = new StructureEdit("batch", BatchItems: body.Items.Select(item => new StructureBatchItem(item.ResourceId, item.Action, item.ExpectedUpdatedAt)).ToArray());
            var result = await operations.ExecuteAsync(await Scope(identity, body.TenantId, ct), id, idempotencyKey, edit, ct);
            return Results.Json(new { success = result.Status == "completed", data = result, message = $"Operation {result.Id}: {result.Status}. {result.Reason}" }, statusCode: result.Status == "completed" ? 200 : result.Status == "pending" ? 202 : 409);
        });
        app.MapOperatorOperations();
        return app;
    }
}
public sealed record GateSlotClaim(Guid TenantId, string VehicleType, Guid? PreferredSlotId = null, Guid? TargetUnitId = null, bool AllowReservedSlot = false);
public sealed record GateSlotRelease(Guid TenantId);
[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record BackupMarkBody(string Reason);
public sealed record OperationBody(Guid IdempotencyKey, StructureEdit Edit, Guid? TenantId = null);
public sealed record CreateSite(string Code, string Name, string Address, decimal? Latitude, decimal? Longitude, Guid? TenantId = null);
public sealed record UnitBody(UnitType Type, string Name, Guid? ParentId, int Capacity, Guid? TenantId = null);
public sealed record SlotBody(Guid UnitId, string Code, VehicleType? VehicleType, SlotType Type = SlotType.STANDARD, IReadOnlyList<VehicleType>? VehicleTypes = null, VehicleType? PrimaryVehicleType = null, Guid? TenantId = null);
public sealed record PathBody(string Code, Guid? From, Guid? To, string? MapData, Guid? TenantId = null);
public sealed record EditStructure(string Action, Guid ResourceId, Guid UnitId, string? Code, string? Name, string? Address, bool Active, int Capacity, VehicleType? VehicleType, SlotType SlotType, IReadOnlyList<VehicleType>? VehicleTypes = null, VehicleType? PrimaryVehicleType = null, string? Coordinates3D = null, string? Features = null, decimal? Latitude = null, decimal? Longitude = null, Guid? TenantId = null);
public sealed record MaintenanceWindowBody(DateTime Start, DateTime End, string Reason, Guid? TenantId = null);
public sealed record MaintenanceWindowPatch(DateTime? Start, DateTime? End, Guid? TenantId = null);
public sealed record BatchBody(IReadOnlyList<BatchItemBody> Items, Guid? IdempotencyKey = null, Guid? TenantId = null);
public sealed record BatchItemBody(Guid ResourceId, string Action, DateTime? ExpectedUpdatedAt = null);
public partial class Program { public static void Main(string[] args) => FacilityHost.Build(args).Run(); }
