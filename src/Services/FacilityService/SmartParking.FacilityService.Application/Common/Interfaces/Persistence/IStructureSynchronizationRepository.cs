namespace SmartParking.FacilityService.Application.Common.Interfaces.Persistence;

public sealed record StructureSynchronization(
    Guid Token, Guid TenantId, Guid SiteId, Guid? OperationId, string State, StructureOutcome? Outcome);

public interface IStructureSynchronizationRepository
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
