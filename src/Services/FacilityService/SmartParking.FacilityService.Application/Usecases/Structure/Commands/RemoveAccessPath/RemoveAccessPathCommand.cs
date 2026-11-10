using MediatR;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.RemoveAccessPath;

public sealed record RemoveAccessPathCommand(
    Guid SiteId,
    Guid PathId,
    Guid? TenantId = null) : IRequest;
