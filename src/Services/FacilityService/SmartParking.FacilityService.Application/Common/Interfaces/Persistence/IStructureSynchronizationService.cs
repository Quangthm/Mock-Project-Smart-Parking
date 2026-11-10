namespace SmartParking.FacilityService.Application.Common.Interfaces.Persistence;

public interface IStructureSynchronizationService
{
    Task RecoverSiteAsync(Guid siteId, CancellationToken cancellationToken);
    Task PublishAsync(StructureSynchronization synchronization, CancellationToken cancellationToken);
    Task<IReadOnlyList<(Guid SiteId, Exception Error)>> RecoverPendingAsync(CancellationToken cancellationToken);
}
