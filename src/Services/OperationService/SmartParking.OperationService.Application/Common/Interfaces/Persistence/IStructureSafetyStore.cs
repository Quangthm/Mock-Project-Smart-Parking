using SmartParking.OperationService.Domain;

namespace SmartParking.OperationService.Application.Common.Interfaces.Persistence;

public interface IStructureSafetyStore
{
    Task<HoldSnapshot> ReadSnapshotAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken);
    Task<HoldSnapshot> AcquireAsync(Guid tenantId, Guid siteId, Guid token, CancellationToken cancellationToken);
    Task ReleaseAsync(Guid tenantId, Guid siteId, Guid token, EditOutcome? outcome, CancellationToken cancellationToken);
    Task<object> ImpactAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken);
}
