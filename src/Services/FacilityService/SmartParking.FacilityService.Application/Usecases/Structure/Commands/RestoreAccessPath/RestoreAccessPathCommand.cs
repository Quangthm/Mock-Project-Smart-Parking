using MediatR;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.RestoreAccessPath;

public sealed record RestoreAccessPathCommand(
    Guid SiteId,
    Guid PathId,
    Guid? TenantId = null) : IRequest;
