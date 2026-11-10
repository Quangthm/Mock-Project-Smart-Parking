using MediatR;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.AddAccessPath;

public sealed record AddAccessPathCommand(
    Guid SiteId,
    string Code,
    Guid? From,
    Guid? To,
    string? MapData = null,
    Guid? TenantId = null) : IRequest<Guid>;
