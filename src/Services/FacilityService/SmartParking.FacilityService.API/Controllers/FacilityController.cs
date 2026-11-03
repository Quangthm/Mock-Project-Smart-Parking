using Microsoft.AspNetCore.Mvc;
using SmartParking.FacilityService.Application;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.API.Controllers;

[ApiController]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class FacilityController(
    IOwnerScopeReader owners,
    ISlotOperatorAuthorizer operators,
    ParkingStructureService structures,
    FacilityDirectoryService directory,
    OwnerOnboardingService onboarding,
    OperatorGateSlotService gateSlots,
    StructureOperationService operations,
    OperatorLayoutService operatorLayout,
    MaintenanceWindowService windows) : ControllerBase
{
    [HttpGet("/health")]
    public IActionResult Health() => Ok(new { status = "ok" });

    [HttpPut("/internal/owner-onboarding/{id:guid}")]
    public async Task<IActionResult> ProvisionOwner(Guid id, OwnerOnboardingBody body, CancellationToken ct)
    {
        await onboarding.ProvisionAsync(id, body, ct);
        return NoContent();
    }

    [HttpPut("/internal/tenants/{id:guid}")]
    public async Task<IActionResult> ProvisionTenant(Guid id, TenantBody body, CancellationToken ct) =>
        await directory.ProvisionTenantAsync(id, body, ct) ? NoContent() : Conflict();

    [HttpPost("/internal/sites/validate")]
    public async Task<IActionResult> ValidateSites(SiteValidation body, CancellationToken ct) =>
        Ok(await directory.ValidateSitesAsync(body, ct));

    [HttpPost("/internal/gate-sites/{siteId:guid}/slots/claim")]
    public async Task<IActionResult> ClaimSlot(Guid siteId, GateSlotClaim body, CancellationToken ct)
    {
        if (body.TenantId == Guid.Empty || siteId == Guid.Empty ||
            body.VehicleType is not ("CAR" or "MOTORCYCLE" or "ANY"))
            return BadRequest();
        return Ok(await gateSlots.ClaimAvailableAsync(body.TenantId, siteId, body.VehicleType,
            body.PreferredSlotId, body.TargetUnitId, body.AllowReservedSlot, ct));
    }

    [HttpPost("/internal/gate-sites/{siteId:guid}/slots/{slotId:guid}/release")]
    public async Task<IActionResult> ReleaseSlot(Guid siteId, Guid slotId, GateSlotRelease body, CancellationToken ct)
    {
        if (body.TenantId == Guid.Empty || siteId == Guid.Empty || slotId == Guid.Empty)
            return BadRequest();
        await gateSlots.ReleaseAsync(body.TenantId, siteId, slotId, ct);
        return NoContent();
    }

    [HttpGet("/api/parking-lots")]
    public async Task<IActionResult> List([FromQuery] Guid? tenantId, CancellationToken ct) =>
        Ok(new { success = true, data = await structures.ListAsync(await ScopeAsync(tenantId, ct), ct) });

    [HttpPost("/api/parking-lots")]
    public async Task<IActionResult> Create(CreateSite body, CancellationToken ct)
    {
        var site = await structures.CreateAsync(await ScopeAsync(body.TenantId, ct),
            body.Code, body.Name, body.Address, body.Latitude, body.Longitude, ct);
        return Created($"/api/parking-lots/{site.Id}/structure", new { success = true, data = site });
    }

    [HttpGet("/api/parking-lots/{id:guid}/structure")]
    public async Task<IActionResult> Get(Guid id, [FromQuery] Guid? tenantId, CancellationToken ct)
    {
        var owner = await ScopeAsync(tenantId, ct);
        var structure = await structures.GetAsync(owner, id, ct);
        var serviceState = await windows.StateAsync(owner, id, structure.Site.Status, ct);
        return Ok(new
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
    }

    [HttpGet("/api/parking-lots/{id:guid}/operational-layout")]
    public async Task<IActionResult> OperationalLayout(Guid id, [FromQuery] string? vehicleType,
        [FromQuery] string? floor, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        Response.Headers.Vary = "Authorization";
        VehicleType? filter = null;
        if (!string.IsNullOrWhiteSpace(vehicleType))
        {
            if (!Enum.TryParse<VehicleType>(vehicleType, false, out var parsed) ||
                !Enum.IsDefined(parsed) || !string.Equals(vehicleType, parsed.ToString(), StringComparison.Ordinal))
                return BadRequest(new { success = false, code = "INVALID_VEHICLE_TYPE",
                    message = "vehicleType must be CAR, MOTORCYCLE, or OVERSIZED." });
            filter = parsed;
        }
        try
        {
            return Ok(new { success = true, data = await structures.ReadOperationalAsync(id, filter, floor, ct) });
        }
        catch (StructureException)
        {
            // Already mapped to 401/403/404/409/... by the outer middleware.
            throw;
        }
        catch (OperationCanceledException)
        {
            // The client aborted; this is not a server failure.
            throw;
        }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                code = "INTERNAL_ERROR",
                message = "An unexpected error occurred."
            });
        }
    }

    [HttpPost("/api/parking-lots/{id:guid}/slots/{slot:guid}/backup")]
    public async Task<IActionResult> MarkBackup(Guid id, Guid slot, BackupMarkBody body, CancellationToken ct)
    {
        await structures.MarkOperationalBackupAsync(await operators.ScopeAsync(id, ct), id, slot, body.Reason, ct);
        return Ok(new { success = true });
    }

    [HttpPost("/api/parking-lots/{id:guid}/operations")]
    public async Task<IActionResult> Execute(Guid id, OperationBody body, CancellationToken ct)
    {
        var result = await operations.ExecuteAsync(await ScopeAsync(body.TenantId, ct), id,
            body.IdempotencyKey, body.Edit, ct);
        return StatusCode(result.Status == "completed" ? 200 : result.Status == "pending" ? 202 : 409,
            new { success = result.Status == "completed", data = result,
                message = $"Operation {result.Id}: {result.Status}. {result.Reason}" });
    }

    [HttpGet("/api/parking-lots/{id:guid}/operations/{operationId:guid}")]
    public async Task<IActionResult> ReadOperation(Guid id, Guid operationId, [FromQuery] Guid? tenantId, CancellationToken ct) =>
        Ok(new { success = true, data = await operations.ReadAsync(await ScopeAsync(tenantId, ct), id, operationId, ct) });

    [HttpPost("/api/parking-lots/{id:guid}/units")]
    public async Task<IActionResult> AddUnit(Guid id, UnitBody body, CancellationToken ct) =>
        Ok(new { success = true, data = await structures.AddUnitAsync(await ScopeAsync(body.TenantId, ct),
            id, body.Type, body.Name, body.ParentId, body.Capacity, ct) });

    [HttpPost("/api/parking-lots/{id:guid}/slots")]
    public async Task<IActionResult> AddSlot(Guid id, SlotBody body, CancellationToken ct) =>
        Ok(new { success = true, data = await structures.AddSlotAsync(await ScopeAsync(body.TenantId, ct),
            id, body.UnitId, body.Code, body.VehicleType, body.Type, body.VehicleTypes, body.PrimaryVehicleType, ct) });

    [HttpPatch("/api/parking-lots/{id:guid}/structure")]
    public async Task<IActionResult> Edit(Guid id, EditStructure body, CancellationToken ct)
    {
        if (body.Action == "backup") await owners.ScopeAsync(ct, "BACKUP_CONFIGURE");
        if (body.Action == "markBackup") await owners.ScopeAsync(ct, "BACKUP_MARK");
        var edit = new StructureEdit(body.Action, body.ResourceId, body.UnitId, body.Code, body.Name,
            body.Address, body.Active, body.Capacity, body.VehicleType, body.SlotType,
            body.Coordinates3D, body.Features, body.Latitude, body.Longitude,
            body.VehicleTypes, body.PrimaryVehicleType);
        await structures.ApplyAsync(await ScopeAsync(body.TenantId, ct), id, edit, ct);
        return Ok(new { success = true });
    }

    [HttpPost("/api/parking-lots/{id:guid}/paths")]
    public async Task<IActionResult> AddPath(Guid id, PathBody body, CancellationToken ct) =>
        Ok(new { success = true, data = await structures.AddAccessPathAsync(await ScopeAsync(body.TenantId, ct),
            id, body.Code, body.From, body.To, body.MapData, ct) });

    [HttpGet("/api/parking-lots/{id:guid}/operator-layout")]
    public async Task<IActionResult> OperatorLayout(Guid id, CancellationToken ct)
    {
        await operators.ScopeAsync(id, ct);
        var structure = await operatorLayout.ReadAsync(id, ct);
        // Explicit projection: the aggregate is not serialized directly, so the released
        // (soft-deleted) collections never reach the wire.
        return Ok(new
        {
            success = true,
            data = new
            {
                structure.Site,
                structure.Units,
                structure.Slots,
                structure.Paths,
                structure.BackupPolicies,
                structure.CapacityViews
            }
        });
    }

    [HttpDelete("/api/parking-lots/{id:guid}/slots/{slot:guid}/backup")]
    public async Task<IActionResult> ReleaseBackup(Guid id, Guid slot, CancellationToken ct)
    {
        await structures.SetOperationalBackupAsync(await operators.ScopeAsync(id, ct), id, slot,
            false, "Backup designation released by operator.", ct);
        return Ok(new { success = true });
    }

    [HttpGet("/api/parking-lots/{id:guid}/maintenance-windows")]
    public async Task<IActionResult> ListMaintenanceWindows(Guid id, [FromQuery] Guid? tenantId, CancellationToken ct) =>
        Ok(new { success = true, data = await windows.ListAsync(await ScopeAsync(tenantId, ct), id, ct) });

    [HttpPost("/api/parking-lots/{id:guid}/maintenance-windows")]
    public async Task<IActionResult> CreateMaintenanceWindow(Guid id, MaintenanceWindowBody body, CancellationToken ct)
    {
        var window = await windows.CreateAsync(await ScopeAsync(body.TenantId, ct), id,
            body.Start, body.End, body.Reason, null, null, ct);
        return Created($"/api/parking-lots/{id}/maintenance-windows/{window.Id}",
            new { success = true, data = window });
    }

    [HttpPatch("/api/parking-lots/{id:guid}/maintenance-windows/{windowId:guid}")]
    public async Task<IActionResult> ExtendMaintenanceWindow(Guid id, Guid windowId,
        MaintenanceWindowPatch body, CancellationToken ct) =>
        Ok(new
        {
            success = true,
            data = await windows.ExtendAsync(await ScopeAsync(body.TenantId, ct), id, windowId,
                body.Start, body.End, ct)
        });

    [HttpDelete("/api/parking-lots/{id:guid}/maintenance-windows/{windowId:guid}")]
    public async Task<IActionResult> CancelMaintenanceWindow(Guid id, Guid windowId,
        [FromQuery] Guid? tenantId, CancellationToken ct)
    {
        await windows.CancelAsync(await ScopeAsync(tenantId, ct), id, windowId, ct);
        return Ok(new { success = true });
    }

    [HttpPost("/api/parking-lots/{id:guid}/structure/batch")]
    public async Task<IActionResult> Batch(Guid id, BatchBody body, CancellationToken ct)
    {
        var header = Request.Headers["Idempotency-Key"].ToString();
        if (!Guid.TryParse(header, out var idempotencyKey))
            return BadRequest(new { success = false, code = "INVALID_STRUCTURE", message = "Idempotency-Key header is required." });
        if (body.IdempotencyKey is { } declared && declared != idempotencyKey)
            return BadRequest(new { success = false, code = "INVALID_STRUCTURE", message = "Idempotency-Key header and body idempotencyKey must agree." });
        var edit = new StructureEdit("batch", BatchItems: body.Items
            .Select(item => new StructureBatchItem(item.ResourceId, item.Action, item.ExpectedUpdatedAt))
            .ToArray());
        var result = await operations.ExecuteAsync(await ScopeAsync(body.TenantId, ct), id,
            idempotencyKey, edit, ct);
        return StatusCode(result.Status == "completed" ? 200 : result.Status == "pending" ? 202 : 409,
            new { success = result.Status == "completed", data = result,
                message = $"Operation {result.Id}: {result.Status}. {result.Reason}" });
    }

    private async Task<OwnerScope> ScopeAsync(Guid? tenantId, CancellationToken ct)
    {
        var scope = await owners.ScopeAsync(ct);
        var selected = tenantId ?? (scope.TenantIds.Length == 1 ? scope.TenantIds[0] : Guid.Empty);
        if (!scope.TenantIds.Contains(selected))
            throw new StructureException("FORBIDDEN", "Select an authorized tenant.");
        return new(scope.UserId, selected);
    }
}
