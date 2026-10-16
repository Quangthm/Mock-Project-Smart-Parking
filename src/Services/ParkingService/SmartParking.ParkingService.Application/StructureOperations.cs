using SmartParking.ParkingService.Domain;

namespace SmartParking.ParkingService.Application;

public sealed record StructureEdit(
    string Action,
    Guid ResourceId = default,
    Guid UnitId = default,
    string? Code = null,
    string? Name = null,
    string? Address = null,
    bool Active = false,
    int Capacity = 0,
    VehicleType VehicleType = default,
    SlotType SlotType = default,
    string? Coordinates3D = null,
    string? Features = null,
    decimal? Latitude = null,
    decimal? Longitude = null);

public sealed record StructureOperation(Guid Id, Guid SiteId, string Status, string? Reason, string? Impact);
public sealed record ReallocationPlan(string Status, string? Reason, string Impact);

public interface IStructureImpactPlanner
{
    Task<ReallocationPlan> PlanAsync(
        OwnerScope owner, Guid siteId, Guid operationId, StructureEdit edit, CancellationToken cancellationToken);
}

public interface IStructureOperationStore
{
    Task<IAsyncDisposable> LockAsync(Guid id, CancellationToken cancellationToken);
    Task<StructureOperation> BeginAsync(OwnerScope owner, Guid site, Guid key, StructureEdit edit, CancellationToken cancellationToken);
    Task<StructureOperation> UpdateAsync(
        OwnerScope owner, Guid id, string status, string? reason, string? impact, CancellationToken cancellationToken);
    Task<StructureOperation> ReadAsync(OwnerScope owner, Guid site, Guid id, CancellationToken cancellationToken);
}

public sealed class StructureOperationService(
    IStructureOperationStore store,
    IStructureImpactPlanner planner,
    ParkingStructureService structure,
    IOwnerAuthorizer owners)
{
    public async Task<StructureOperation> ReadAsync(
        OwnerScope owner, Guid site, Guid id, CancellationToken cancellationToken)
    {
        await AuthorizeAsync(owner, cancellationToken);
        return await store.ReadAsync(owner, site, id, cancellationToken);
    }

    public async Task<StructureOperation> ExecuteAsync(
        OwnerScope owner, Guid site, Guid key, StructureEdit edit, CancellationToken cancellationToken)
    {
        await AuthorizeAsync(owner, cancellationToken);
        if (key == Guid.Empty)
            throw new StructureException("INVALID_STRUCTURE", "Provide an idempotency key.");
        if (edit.Action is not ("removeSlot" or "removeUnit" or "capacity" or "active" or "moveSlot" or "configureSlot"))
            throw new StructureException("INVALID_STRUCTURE", "Unsupported impact operation.");

        var operation = await store.BeginAsync(owner, site, key, edit, cancellationToken);
        await using var gate = await store.LockAsync(operation.Id, cancellationToken);
        operation = await store.ReadAsync(owner, site, operation.Id, cancellationToken);
        if (operation.Status == "completed") return operation;

        // A previous attempt may have committed physically but lost its response.
        // Recovery publishes the durable outcome before we plan any new changes.
        await structure.GetAsync(owner, site, cancellationToken);
        operation = await store.ReadAsync(owner, site, operation.Id, cancellationToken);
        if (operation.Status == "completed") return operation;

        var plan = await planner.PlanAsync(owner, site, operation.Id, edit, cancellationToken);
        if (plan.Status != "resolved")
        {
            return await store.UpdateAsync(
                owner, operation.Id, plan.Status == "failed" ? "failed" : "pending",
                plan.Reason, plan.Impact, cancellationToken);
        }

        await store.UpdateAsync(
            owner, operation.Id, "pending",
            "Impact resolved; awaiting physical commit and projection acknowledgement.",
            plan.Impact, cancellationToken);

        try
        {
            // The Domain rechecks the change under the normal hold and local row
            // locks. A planner response by itself does not authorize eviction.
            await structure.ChangeAsync(
                owner, site, aggregate => StructureEditHandler.Apply(aggregate, edit),
                cancellationToken, operation.Id);

            // Synchronization acknowledgement already completed this operation
            // atomically. Updating it again would race with recovery.
            return await store.ReadAsync(owner, site, operation.Id, cancellationToken);
        }
        catch (StructureException error)
        {
            var status = error.Code is "STRUCTURE_IN_USE" or "STRUCTURE_BUSY" or "COMMITMENTS_UNAVAILABLE"
                ? "pending" : "failed";
            return await store.UpdateAsync(
                owner, operation.Id, status, error.Message, plan.Impact, cancellationToken);
        }
    }

    private async Task AuthorizeAsync(OwnerScope owner, CancellationToken cancellationToken)
    {
        if (!await owners.IsOwnerAsync(owner, cancellationToken))
            throw new StructureException("FORBIDDEN", "Current Owner access is required.");
    }
}
