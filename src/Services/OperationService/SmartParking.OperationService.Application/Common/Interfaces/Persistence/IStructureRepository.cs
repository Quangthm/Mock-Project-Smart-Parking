using SmartParking.OperationService.Domain;
using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Application.Common.Interfaces.Persistence;

public interface IOperationsConcurrency
{
    Task LockSiteAsync(Guid siteId, CancellationToken ct);
    Task LockVehicleAsync(string identity, CancellationToken ct);
}

public interface IStructureRepository
{
    Task<StructureEditHoldRow?> HoldAsync(Guid siteId, CancellationToken ct);
    Task<StructureReleaseReceiptRow?> ReceiptAsync(Guid token, CancellationToken ct);
    Task<StructureSiteStateRow?> StateAsync(Guid siteId, CancellationToken ct);
    Task<IReadOnlyList<SiteCapacityPoolRow>> PoolsAsync(Guid tenantId, Guid siteId, CancellationToken ct);
    Task<HoldSnapshot> SnapshotAsync(Guid tenantId, Guid siteId, DateTimeOffset now, CancellationToken ct);
    Task<StructureImpactResponse> ImpactAsync(Guid tenantId, Guid siteId, DateTimeOffset now, CancellationToken ct);
    void AddHold(StructureEditHoldRow hold);
    void RemoveHold(StructureEditHoldRow hold);
    void AddReceipt(StructureReleaseReceiptRow receipt);
    void AddState(StructureSiteStateRow state);
    Task<StructureMaintenanceClosureRow?> MaintenanceClosureAsync(Guid windowId, CancellationToken ct) =>
        Task.FromResult<StructureMaintenanceClosureRow?>(null);
    void AddMaintenanceClosure(StructureMaintenanceClosureRow closure) =>
        throw new NotSupportedException("Maintenance closures are not supported by this repository.");
    Task<IReadOnlyList<MaintenanceCommitmentConflict>> FindMaintenanceConflictsAsync(
        Guid tenantId,
        Guid siteId,
        Guid? unitId,
        Guid? slotId,
        IReadOnlySet<Guid> scopedUnitIds,
        IReadOnlySet<Guid> scopedSlotIds,
        DateTimeOffset start,
        DateTimeOffset end,
        DateTimeOffset now,
        CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<MaintenanceCommitmentConflict>>([]);
}
