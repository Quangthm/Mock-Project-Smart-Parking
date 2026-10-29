using SmartParking.ReservationService.Domain;

namespace SmartParking.ReservationService.Application.Common.Interfaces.Persistence;

public interface IStructureSafetyStore
{
    Task<HoldSnapshot> ReadSnapshotAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken);
    Task<HoldSnapshot> AcquireAsync(Guid tenantId, Guid siteId, Guid token, CancellationToken cancellationToken);
    Task ReleaseAsync(Guid tenantId, Guid siteId, Guid token, EditOutcome? outcome, CancellationToken cancellationToken);
    // Site-level repair for an abandoned preparation whose leaked hold token no
    // longer matches any durable row. Never used while another edit is live.
    Task ForceReleaseAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken);
    Task<object> ImpactAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken);
}
