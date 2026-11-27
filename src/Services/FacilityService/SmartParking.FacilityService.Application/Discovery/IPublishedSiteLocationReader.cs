namespace SmartParking.FacilityService.Application.Discovery;

public interface IPublishedSiteLocationReader
{
    Task<PublishedSiteLocation?> ReadAsync(Guid siteId, CancellationToken ct);
}
