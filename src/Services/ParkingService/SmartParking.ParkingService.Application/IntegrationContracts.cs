namespace SmartParking.ParkingService.Application;

public interface IOwnerAuthorizer
{
    Task<bool> IsOwnerAsync(OwnerScope owner, CancellationToken ct);
}
public sealed record CommitmentSnapshot(Guid[] ProtectedSlots, bool HasLiveCommitments);
public sealed record PoolCapacity(Guid? UnitId, string VehicleType, int Capacity);
public sealed record StructureOutcome(bool SiteActive, Guid[] RemovedSlots, Guid[] RemovedUnits, PoolCapacity[] Capacities);
public interface IStructureLease : IAsyncDisposable
{
    CommitmentSnapshot Snapshot { get; }
    void BeginCommit();
    // Called after Parking DB commit; failure must leave the durable hold closed for reconciliation.
    Task CompleteAsync(StructureOutcome outcome, CancellationToken ct);
}
public interface IStructureCommitments
{
    Task<IStructureLease> AcquireAsync(Guid tenantId, Guid siteId, CancellationToken ct);
}
