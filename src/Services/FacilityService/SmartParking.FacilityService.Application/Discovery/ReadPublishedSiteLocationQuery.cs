using MediatR;

namespace SmartParking.FacilityService.Application.Discovery;

public sealed record ReadPublishedSiteLocationQuery(Guid SiteId) : IRequest<PublishedSiteLocation?>;
