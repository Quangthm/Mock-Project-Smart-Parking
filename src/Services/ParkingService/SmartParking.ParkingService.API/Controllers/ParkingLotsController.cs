using Microsoft.AspNetCore.Mvc;
using SmartParking.ParkingService.API.Controllers.Base;
using SmartParking.ParkingService.Application;
using SmartParking.ParkingService.Domain;
using SmartParking.ParkingService.Infrastructure;

namespace SmartParking.ParkingService.API.Controllers;

[Route("api/parking-lots")]
public sealed class ParkingLotsController(
    OwnerIdentityClient owners,
    SlotOperatorClient operators,
    ParkingStructureService structures,
    StructureOperationService operations,
    OperatorLayoutService layouts) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<object>> List([FromQuery] Guid? tenantId, CancellationToken ct) =>
        Success(await structures.ListAsync(await ScopeAsync(tenantId, ct), ct));

    [HttpPost]
    public async Task<ActionResult<object>> Create(CreateSite body, CancellationToken ct)
    {
        var site = await structures.CreateAsync(
            await ScopeAsync(body.TenantId, ct), body.Code, body.Name, body.Address,
            body.Latitude, body.Longitude, ct);
        return Created($"/api/parking-lots/{site.Id}/structure", new { success = true, data = site });
    }

    [HttpGet("{id:guid}/structure")]
    public async Task<ActionResult<object>> Structure(Guid id, [FromQuery] Guid? tenantId, CancellationToken ct) =>
        Success(await structures.GetAsync(await ScopeAsync(tenantId, ct), id, ct));

    [HttpGet("{id:guid}/operational-layout")]
    public async Task<ActionResult<object>> OperationalLayout(Guid id, CancellationToken ct) =>
        Success(await structures.ReadOperationalAsync(await operators.ScopeAsync(id, ct), id, ct));

    [HttpPost("{id:guid}/slots/{slot:guid}/backup")]
    public async Task<ActionResult<object>> MarkBackup(
        Guid id, Guid slot, BackupMarkBody body, CancellationToken ct)
    {
        await structures.MarkOperationalBackupAsync(
            await operators.ScopeAsync(id, ct), id, slot, body.Reason, ct);
        return Ok(new { success = true });
    }

    [HttpPost("{id:guid}/operations")]
    public async Task<IActionResult> ExecuteOperation(
        Guid id, OperationBody body, CancellationToken ct)
    {
        var result = await operations.ExecuteAsync(
            await ScopeAsync(body.TenantId, ct), id, body.IdempotencyKey, body.Edit, ct);
        return StatusCode(
            result.Status == "completed" ? 200 : result.Status == "pending" ? 202 : 409,
            new
            {
                success = result.Status == "completed",
                data = result,
                message = $"Operation {result.Id}: {result.Status}. {result.Reason}",
            });
    }

    [HttpGet("{id:guid}/operations/{operationId:guid}")]
    public async Task<ActionResult<object>> ReadOperation(
        Guid id, Guid operationId, [FromQuery] Guid? tenantId, CancellationToken ct) =>
        Success(await operations.ReadAsync(await ScopeAsync(tenantId, ct), id, operationId, ct));

    [HttpPost("{id:guid}/units")]
    public async Task<ActionResult<object>> AddUnit(Guid id, UnitBody body, CancellationToken ct) =>
        Success(await structures.AddUnitAsync(
            await ScopeAsync(body.TenantId, ct), id, body.Type, body.Name,
            body.ParentId, body.Capacity, ct));

    [HttpPost("{id:guid}/slots")]
    public async Task<ActionResult<object>> AddSlot(Guid id, SlotBody body, CancellationToken ct) =>
        Success(await structures.AddSlotAsync(
            await ScopeAsync(body.TenantId, ct), id, body.UnitId, body.Code,
            body.VehicleType, body.Type, ct));

    [HttpPatch("{id:guid}/structure")]
    public async Task<ActionResult<object>> ApplyStructure(
        Guid id, EditStructure body, CancellationToken ct)
    {
        if (body.Action == "backup") await owners.ScopeAsync(ct, "BACKUP_CONFIGURE");
        if (body.Action == "markBackup") await owners.ScopeAsync(ct, "BACKUP_MARK");
        var edit = new StructureEdit(
            body.Action, body.ResourceId, body.UnitId, body.Code, body.Name, body.Address,
            body.Active, body.Capacity, body.VehicleType, body.SlotType,
            body.Coordinates3D, body.Features, body.Latitude, body.Longitude);
        await structures.ApplyAsync(await ScopeAsync(body.TenantId, ct), id, edit, ct);
        return Ok(new { success = true });
    }

    [HttpPost("{id:guid}/paths")]
    public async Task<ActionResult<object>> AddPath(Guid id, PathBody body, CancellationToken ct) =>
        Success(await structures.AddAccessPathAsync(
            await ScopeAsync(body.TenantId, ct), id, body.Code, body.From, body.To,
            body.MapData, ct));

    [HttpGet("{id:guid}/operator-layout")]
    public async Task<ActionResult<object>> OperatorLayout(Guid id, CancellationToken ct) =>
        Success(await layouts.ReadAsync(id, ct));

    [HttpDelete("{id:guid}/slots/{slot:guid}/backup")]
    public async Task<ActionResult<object>> ReleaseBackup(Guid id, Guid slot, CancellationToken ct)
    {
        await structures.SetOperationalBackupAsync(
            await operators.ScopeAsync(id, ct), id, slot, false,
            "Backup designation released by operator.", ct);
        return Ok(new { success = true });
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
