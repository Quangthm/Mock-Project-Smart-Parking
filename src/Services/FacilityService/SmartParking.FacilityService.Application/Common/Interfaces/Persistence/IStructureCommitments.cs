using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Common.Interfaces.Persistence;

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
    Task<CommitmentSnapshot> ReadSnapshotAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken);
    Task<CommitmentSnapshot> AcquireAsync(Guid tenantId, Guid siteId, Guid token, CancellationToken cancellationToken);
    Task ReleaseAsync(Guid tenantId, Guid siteId, Guid token, StructureOutcome? outcome, CancellationToken cancellationToken);
    // A site can hold at most one edit hold, and an abandoned preparation leaves no
    // physical change behind. This releases a leaked hold whose token no longer
    // matches any durable row; it is only valid for an aborting row, under the site gate.
    Task ForceReleaseAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken);
}
