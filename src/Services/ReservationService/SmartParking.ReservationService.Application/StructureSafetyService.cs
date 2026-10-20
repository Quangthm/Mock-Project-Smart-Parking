using SmartParking.ReservationService.Domain;

namespace SmartParking.ReservationService.Application;

public interface IStructureSafetyStore
{
    Task<HoldSnapshot> ReadSnapshotAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken);
    Task<HoldSnapshot> AcquireAsync(Guid tenantId, Guid siteId, Guid token, CancellationToken cancellationToken);
    Task ReleaseAsync(Guid tenantId, Guid siteId, Guid token, EditOutcome? outcome, CancellationToken cancellationToken);
    Task<object> ImpactAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken);
}

// API delegates the use case here. Rules remain in Domain; Persistence implements
// local transactions, SQL and the advisory lock shared with reservation writers.
public sealed class StructureSafetyService(IStructureSafetyStore store)
{
    public Task<HoldSnapshot> PrepareAsync(Guid tenantId, Guid siteId, Guid token, CancellationToken ct)
    {
        StructureEditPolicy.ValidateScope(tenantId, siteId, token);
        return store.AcquireAsync(tenantId, siteId, token, ct);
    }

    public Task CompleteAsync(Guid tenantId, Guid siteId, Guid token, EditOutcome? outcome, CancellationToken ct)
    {
        StructureEditPolicy.ValidateScope(tenantId, siteId, token);
        StructureEditPolicy.ValidateOutcome(outcome);
        return store.ReleaseAsync(tenantId, siteId, token, outcome, ct);
    }

    public Task<HoldSnapshot> ReadSnapshotAsync(Guid tenantId, Guid siteId, CancellationToken ct)
    {
        if (tenantId == Guid.Empty || siteId == Guid.Empty)
            throw new InvalidStructureEditException("Tenant and site must be non-empty.");
        return store.ReadSnapshotAsync(tenantId, siteId, ct);
    }

    public Task<object> ImpactAsync(Guid tenantId, Guid siteId, CancellationToken ct)
    {
        if (tenantId == Guid.Empty || siteId == Guid.Empty)
            throw new InvalidStructureEditException("Tenant and site must be non-empty.");
        return store.ImpactAsync(tenantId, siteId, ct);
    }
}
