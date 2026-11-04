using MediatR;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.SetSiteActive;

public sealed record SetSiteActiveCommand(
    Guid SiteId,
    bool Active,
    Guid? TenantId = null) : IRequest;
