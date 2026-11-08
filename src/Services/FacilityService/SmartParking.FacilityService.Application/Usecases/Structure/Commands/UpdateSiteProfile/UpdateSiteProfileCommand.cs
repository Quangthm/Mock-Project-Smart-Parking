using MediatR;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.UpdateSiteProfile;

public sealed record UpdateSiteProfileCommand(
    Guid SiteId,
    string Code,
    string Name,
    string Address,
    decimal? Latitude = null,
    decimal? Longitude = null,
    Guid? TenantId = null) : IRequest;
