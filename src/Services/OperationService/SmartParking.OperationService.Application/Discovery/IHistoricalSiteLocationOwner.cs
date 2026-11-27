namespace SmartParking.OperationService.Application.Discovery;

public interface IHistoricalSiteLocationOwner
{
    Task<FacilitySiteLocation?> ReadPublishedAsync(Guid siteId, CancellationToken ct);
}
