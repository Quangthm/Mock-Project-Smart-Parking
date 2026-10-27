using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Domain;

namespace SmartParking.OperationService.Application;

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
