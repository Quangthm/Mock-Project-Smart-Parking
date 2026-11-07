using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.DTOs;
using SmartParking.FacilityService.Application.Usecases.Gate.Commands.ClaimGateSlot;
using SmartParking.FacilityService.Application.Usecases.Gate.Commands.MarkOperationalBackup;
using SmartParking.FacilityService.Application.Usecases.Gate.Commands.ReleaseGateSlot;
using SmartParking.FacilityService.Application.Usecases.Gate.Commands.ReleaseOperationalBackup;
using SmartParking.FacilityService.Application.Usecases.Internal.Commands.ProvisionOwner;
using SmartParking.FacilityService.Application.Usecases.Internal.Commands.ProvisionTenant;
using SmartParking.FacilityService.Application.Usecases.Internal.Queries.ValidateSites;
using SmartParking.FacilityService.Application.Usecases.Layout.Queries.GetOperationalLayout;
using SmartParking.FacilityService.Application.Usecases.Layout.Queries.GetOperatorLayout;
using SmartParking.FacilityService.Application.Usecases.Lots.Commands.CreateParkingLot;
using SmartParking.FacilityService.Application.Usecases.Lots.Queries.ListParkingLots;
using SmartParking.FacilityService.Application.Usecases.Maintenance.Commands.CancelMaintenanceWindow;
using SmartParking.FacilityService.Application.Usecases.Maintenance.Commands.ExtendMaintenanceWindow;
using SmartParking.FacilityService.Application.Usecases.Maintenance.Commands.ScheduleMaintenanceWindow;
using SmartParking.FacilityService.Application.Usecases.Maintenance.Queries.ListMaintenanceWindows;
using SmartParking.FacilityService.Application.Usecases.Operations.Commands.ExecuteStructureOperation;
using SmartParking.FacilityService.Application.Usecases.Operations.Queries.GetStructureOperation;
using SmartParking.FacilityService.Application.Usecases.Structure.Commands.AddAccessPath;
using SmartParking.FacilityService.Application.Usecases.Structure.Commands.AddSlot;
using SmartParking.FacilityService.Application.Usecases.Structure.Commands.AddUnit;
using SmartParking.FacilityService.Application.Usecases.Structure.Commands.BatchEditStructure;
using SmartParking.FacilityService.Application.Usecases.Structure.Commands.ConfigureBackupPolicy;
using SmartParking.FacilityService.Application.Usecases.Structure.Commands.ConfigureSlot;
using SmartParking.FacilityService.Application.Usecases.Structure.Commands.MarkBackup;
using SmartParking.FacilityService.Application.Usecases.Structure.Commands.MoveSlot;
using SmartParking.FacilityService.Application.Usecases.Structure.Commands.RemoveAccessPath;
using SmartParking.FacilityService.Application.Usecases.Structure.Commands.RemoveSlot;
using SmartParking.FacilityService.Application.Usecases.Structure.Commands.RemoveUnit;
using SmartParking.FacilityService.Application.Usecases.Structure.Commands.RenameUnit;
using SmartParking.FacilityService.Application.Usecases.Structure.Commands.RestoreAccessPath;
using SmartParking.FacilityService.Application.Usecases.Structure.Commands.RestoreSlot;
using SmartParking.FacilityService.Application.Usecases.Structure.Commands.RestoreUnit;
using SmartParking.FacilityService.Application.Usecases.Structure.Commands.SetSiteActive;
using SmartParking.FacilityService.Application.Usecases.Structure.Commands.SetUnitCapacity;
using SmartParking.FacilityService.Application.Usecases.Structure.Commands.UpdateSiteProfile;
using SmartParking.FacilityService.Application.Usecases.Structure.Queries.GetParkingStructure;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.API.Controllers;

[ApiController]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class FacilityController(
    IMediator mediator,
    ILogger<FacilityController> logger) : ControllerBase
{
    [HttpGet("/health")]
    public IActionResult Health() => Ok(new { status = "ok" });

    [HttpPut("/internal/owner-onboarding/{id:guid}")]
    public async Task<IActionResult> ProvisionOwner(Guid id, OwnerOnboardingBody body, CancellationToken ct)
    {
        await mediator.Send(new ProvisionOwnerCommand(id, body), ct);
        return NoContent();
    }

    [HttpPut("/internal/tenants/{id:guid}")]
    public async Task<IActionResult> ProvisionTenant(Guid id, TenantBody body, CancellationToken ct)
    {
        var result = await mediator.Send(new ProvisionTenantCommand(id, body), ct);
        return result ? NoContent() : Conflict();
    }

    [HttpPost("/internal/sites/validate")]
    public async Task<IActionResult> ValidateSites(SiteValidation body, CancellationToken ct)
    {
        var result = await mediator.Send(new ValidateSitesQuery(body), ct);
        return Ok(result);
    }

    [HttpPost("/internal/gate-sites/{siteId:guid}/slots/claim")]
    public async Task<IActionResult> ClaimSlot(Guid siteId, GateSlotClaim body, CancellationToken ct)
    {
        if (body.TenantId == Guid.Empty || siteId == Guid.Empty ||
            body.VehicleType is not ("CAR" or "MOTORCYCLE" or "ANY"))
            return BadRequest();
        var result = await mediator.Send(new ClaimGateSlotCommand(
            body.TenantId, siteId, body.VehicleType, body.PreferredSlotId, body.TargetUnitId, body.AllowReservedSlot), ct);
        return Ok(result);
    }

    [HttpPost("/internal/gate-sites/{siteId:guid}/slots/{slotId:guid}/release")]
    public async Task<IActionResult> ReleaseSlot(Guid siteId, Guid slotId, GateSlotRelease body, CancellationToken ct)
    {
        if (body.TenantId == Guid.Empty || siteId == Guid.Empty || slotId == Guid.Empty)
            return BadRequest();
        await mediator.Send(new ReleaseGateSlotCommand(body.TenantId, siteId, slotId), ct);
        return NoContent();
    }

    [HttpGet("/api/parking-lots")]
    public async Task<IActionResult> List([FromQuery] Guid? tenantId, CancellationToken ct)
    {
        var result = await mediator.Send(new ListParkingLotsQuery(tenantId), ct);
        return Ok(new { success = true, data = result });
    }

    [HttpPost("/api/parking-lots")]
    public async Task<IActionResult> Create(CreateSite body, CancellationToken ct)
    {
        var site = await mediator.Send(new CreateParkingLotCommand(
            body.Code, body.Name, body.Address, body.Latitude, body.Longitude, body.TenantId), ct);
        return Created($"/api/parking-lots/{site.Id}/structure", new { success = true, data = site });
    }

    [HttpGet("/api/parking-lots/{id:guid}/structure")]
    public async Task<IActionResult> Get(Guid id, [FromQuery] Guid? tenantId, CancellationToken ct)
    {
        var structure = await mediator.Send(new GetParkingStructureQuery(id, tenantId), ct);
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
                    state = structure.ServiceState.State,
                    closedUntil = structure.ServiceState.ClosedUntil,
                    windows = structure.ServiceState.Windows.Select(window => new
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
            var result = await mediator.Send(new GetOperationalLayoutQuery(id, filter, floor), ct);
            return Ok(new { success = true, data = result });
        }
        catch (StructureException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error retrieving operational layout for site {SiteId}", id);
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
        await mediator.Send(new MarkOperationalBackupCommand(id, slot, body.Reason), ct);
        return Ok(new { success = true });
    }

    [HttpPost("/api/parking-lots/{id:guid}/operations")]
    public async Task<IActionResult> Execute(Guid id, OperationBody body, CancellationToken ct)
    {
        var result = await mediator.Send(new ExecuteStructureOperationCommand(
            id, body.IdempotencyKey, body.Edit, body.TenantId), ct);
        return StatusCode(result.Status == "completed" ? 200 : result.Status == "pending" ? 202 : 409,
            new { success = result.Status == "completed", data = result,
                message = $"Operation {result.Id}: {result.Status}. {result.Reason}" });
    }

    [HttpGet("/api/parking-lots/{id:guid}/operations/{operationId:guid}")]
    public async Task<IActionResult> ReadOperation(Guid id, Guid operationId, [FromQuery] Guid? tenantId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetStructureOperationQuery(id, operationId, tenantId), ct);
        return Ok(new { success = true, data = result });
    }

    [HttpPost("/api/parking-lots/{id:guid}/units")]
    public async Task<IActionResult> AddUnit(Guid id, UnitBody body, CancellationToken ct)
    {
        var unitId = await mediator.Send(new AddUnitCommand(
            id, body.Type, body.Name, body.ParentId, body.Capacity, body.TenantId), ct);
        return Ok(new { success = true, data = unitId });
    }

    [HttpPost("/api/parking-lots/{id:guid}/slots")]
    public async Task<IActionResult> AddSlot(Guid id, SlotBody body, CancellationToken ct)
    {
        var slotId = await mediator.Send(new AddSlotCommand(
            id, body.UnitId, body.Code, body.VehicleType, body.Type,
            body.VehicleTypes, body.PrimaryVehicleType, body.TenantId), ct);
        return Ok(new { success = true, data = slotId });
    }

    [HttpPatch("/api/parking-lots/{id:guid}/structure")]
    public async Task<IActionResult> Edit(Guid id, EditStructure body, CancellationToken ct)
    {
        switch (body.Action)
        {
            case "profile" or "siteProfile":
                await mediator.Send(new UpdateSiteProfileCommand(
                    id, body.Code ?? string.Empty, body.Name ?? string.Empty, body.Address ?? string.Empty,
                    body.Latitude, body.Longitude, body.TenantId), ct);
                break;
            case "active" or "siteActive":
                await mediator.Send(new SetSiteActiveCommand(id, body.Active, body.TenantId), ct);
                break;
            case "renameUnit":
                var renameUnitId = body.UnitId != Guid.Empty ? body.UnitId : body.ResourceId;
                await mediator.Send(new RenameUnitCommand(id, renameUnitId, body.Name ?? string.Empty, body.TenantId), ct);
                break;
            case "capacity" or "setUnitCapacity":
                var capacityUnitId = body.UnitId != Guid.Empty ? body.UnitId : body.ResourceId;
                await mediator.Send(new SetUnitCapacityCommand(id, capacityUnitId, body.Capacity, body.TenantId), ct);
                break;
            case "removeUnit":
                var removeUnitId = body.ResourceId != Guid.Empty ? body.ResourceId : body.UnitId;
                await mediator.Send(new RemoveUnitCommand(id, removeUnitId, body.TenantId), ct);
                break;
            case "restoreUnit":
                var restoreUnitId = body.ResourceId != Guid.Empty ? body.ResourceId : body.UnitId;
                await mediator.Send(new RestoreUnitCommand(id, restoreUnitId, body.TenantId), ct);
                break;
            case "removeSlot":
                await mediator.Send(new RemoveSlotCommand(id, body.ResourceId, body.TenantId), ct);
                break;
            case "restoreSlot":
                await mediator.Send(new RestoreSlotCommand(id, body.ResourceId, body.TenantId), ct);
                break;
            case "moveSlot":
                await mediator.Send(new MoveSlotCommand(id, body.ResourceId, body.UnitId, body.Code ?? string.Empty, body.TenantId), ct);
                break;
            case "configureSlot":
                await mediator.Send(new ConfigureSlotCommand(
                    id, body.ResourceId, body.VehicleType, body.SlotType, body.Coordinates3D, body.Features,
                    body.VehicleTypes, body.PrimaryVehicleType, body.TenantId), ct);
                break;
            case "backup":
                await mediator.Send(new ConfigureBackupPolicyCommand(
                    id, body.UnitId == Guid.Empty ? null : body.UnitId, body.VehicleType, body.Capacity, body.TenantId), ct);
                break;
            case "markBackup":
                await mediator.Send(new MarkBackupCommand(id, body.ResourceId, body.Active, body.TenantId), ct);
                break;
            case "removePath":
                await mediator.Send(new RemoveAccessPathCommand(id, body.ResourceId, body.TenantId), ct);
                break;
            case "restorePath":
                await mediator.Send(new RestoreAccessPathCommand(id, body.ResourceId, body.TenantId), ct);
                break;
            default:
                throw new StructureException("INVALID_STRUCTURE", $"Unsupported edit action: {body.Action}");
        }
        return Ok(new { success = true });
    }

    [HttpPost("/api/parking-lots/{id:guid}/paths")]
    public async Task<IActionResult> AddPath(Guid id, PathBody body, CancellationToken ct)
    {
        var pathId = await mediator.Send(new AddAccessPathCommand(
            id, body.Code, body.From, body.To, body.MapData, body.TenantId), ct);
        return Ok(new { success = true, data = pathId });
    }

    [HttpGet("/api/parking-lots/{id:guid}/operator-layout")]
    public async Task<IActionResult> OperatorLayout(Guid id, CancellationToken ct)
    {
        var structure = await mediator.Send(new GetOperatorLayoutQuery(id), ct);
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
        await mediator.Send(new ReleaseOperationalBackupCommand(id, slot), ct);
        return Ok(new { success = true });
    }

    [HttpGet("/api/parking-lots/{id:guid}/maintenance-windows")]
    public async Task<IActionResult> ListMaintenanceWindows(Guid id, [FromQuery] Guid? tenantId, CancellationToken ct)
    {
        var windows = await mediator.Send(new ListMaintenanceWindowsQuery(id, tenantId), ct);
        return Ok(new { success = true, data = windows });
    }

    [HttpPost("/api/parking-lots/{id:guid}/maintenance-windows")]
    public async Task<IActionResult> CreateMaintenanceWindow(Guid id, MaintenanceWindowBody body, CancellationToken ct)
    {
        var window = await mediator.Send(new ScheduleMaintenanceWindowCommand(
            id, body.Start, body.End, body.Reason, body.TenantId), ct);
        return Created($"/api/parking-lots/{id}/maintenance-windows/{window.Id}",
            new { success = true, data = window });
    }

    [HttpPatch("/api/parking-lots/{id:guid}/maintenance-windows/{windowId:guid}")]
    public async Task<IActionResult> ExtendMaintenanceWindow(Guid id, Guid windowId,
        MaintenanceWindowPatch body, CancellationToken ct)
    {
        var window = await mediator.Send(new ExtendMaintenanceWindowCommand(
            id, windowId, body.Start, body.End, body.TenantId), ct);
        return Ok(new { success = true, data = window });
    }

    [HttpDelete("/api/parking-lots/{id:guid}/maintenance-windows/{windowId:guid}")]
    public async Task<IActionResult> CancelMaintenanceWindow(Guid id, Guid windowId,
        [FromQuery] Guid? tenantId, CancellationToken ct)
    {
        await mediator.Send(new CancelMaintenanceWindowCommand(id, windowId, tenantId), ct);
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

        var result = await mediator.Send(new BatchEditStructureCommand(id, idempotencyKey, body.Items, body.TenantId), ct);
        return StatusCode(result.Status == "completed" ? 200 : result.Status == "pending" ? 202 : 409,
            new { success = result.Status == "completed", data = result,
                message = $"Operation {result.Id}: {result.Status}. {result.Reason}" });
    }
}
