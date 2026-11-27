using MediatR;

namespace SmartParking.FacilityService.Application.Discovery;

public sealed class ReadPublishedSiteLocationHandler(IPublishedSiteLocationReader reader)
    : IRequestHandler<ReadPublishedSiteLocationQuery, PublishedSiteLocation?>
{
    public Task<PublishedSiteLocation?> Handle(ReadPublishedSiteLocationQuery query, CancellationToken ct) =>
        reader.ReadAsync(query.SiteId, ct);
}
