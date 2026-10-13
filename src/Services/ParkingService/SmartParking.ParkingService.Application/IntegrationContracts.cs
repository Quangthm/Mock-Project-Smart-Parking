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
    Task<IStructureLease> AcquireAsync(Guid tenantId, Guid siteId, CancellationToken ct);
    Task<CommitmentSnapshot> ReadSnapshotAsync(Guid tenantId, Guid siteId, CancellationToken ct);
}
