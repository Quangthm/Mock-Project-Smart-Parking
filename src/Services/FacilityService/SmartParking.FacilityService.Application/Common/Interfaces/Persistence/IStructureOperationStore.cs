using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Common.Interfaces.Persistence;

public sealed record StructureEdit(
    string Action,
    Guid ResourceId = default,
    Guid UnitId = default,
    string? Code = null,
    string? Name = null,
    string? Address = null,
    bool Active = false,
    int Capacity = 0,
    VehicleType? VehicleType = null,
    SlotType SlotType = default,
    string? Coordinates3D = null,
    string? Features = null,
    decimal? Latitude = null,
    decimal? Longitude = null,
    IReadOnlyList<VehicleType>? VehicleTypes = null,
    VehicleType? PrimaryVehicleType = null,
    IReadOnlyList<StructureBatchItem>? BatchItems = null);

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
