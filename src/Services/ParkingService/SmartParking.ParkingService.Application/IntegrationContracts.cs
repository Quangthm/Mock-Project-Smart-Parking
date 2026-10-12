using SmartParking.ParkingService.Domain;

namespace SmartParking.ParkingService.Application;

public interface IOwnerAuthorizer
{
    Task<bool> IsOwnerAsync(OwnerScope owner, CancellationToken cancellationToken);
}

public sealed record CommitmentSnapshot(
    Guid[] ProtectedSlots,
    bool HasLiveCommitments,
    CapacityClaim[]? Capacities = null);

public sealed record PoolCapacity(Guid? UnitId, string VehicleType, int Capacity, int Backup = 0);
public sealed record StructureOutcome(
    bool SiteActive, Guid[] RemovedSlots, Guid[] RemovedUnits, PoolCapacity[] Capacities);

// Tokens are created and persisted by Application before the first HTTP request.
// Infrastructure must never replace a token when retrying an uncertain request.
public interface IStructureCommitments
{
    Task<CommitmentSnapshot> AcquireAsync(Guid tenantId, Guid siteId, Guid token, CancellationToken cancellationToken);
    Task ReleaseAsync(Guid tenantId, Guid siteId, Guid token, StructureOutcome? outcome, CancellationToken cancellationToken);
}

public sealed record StructureSynchronization(
    Guid Token, Guid TenantId, Guid SiteId, Guid? OperationId, string State, StructureOutcome? Outcome);

public interface IStructureSynchronizationStore
{
    // This is a session lock, not an open transaction. It also excludes the recovery worker.
    Task<IAsyncDisposable?> TryLockSiteAsync(Guid siteId, CancellationToken cancellationToken);
    Task PrepareAsync(StructureSynchronization synchronization, CancellationToken cancellationToken);
    Task<IReadOnlyList<StructureSynchronization>> PendingAsync(Guid? siteId, CancellationToken cancellationToken);
    // Atomically fence an abandoned preparation before releasing its hold.
    // If a physical commit raced with recovery, return its ready outcome instead.
    Task<StructureSynchronization> FencePreparationAsync(Guid token, CancellationToken cancellationToken);
    Task AcknowledgeAsync(Guid token, CancellationToken cancellationToken);
}
