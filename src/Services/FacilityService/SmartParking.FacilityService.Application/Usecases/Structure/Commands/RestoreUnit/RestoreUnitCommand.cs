using MediatR;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.RestoreUnit;

public sealed record RestoreUnitCommand(
    Guid SiteId,
    Guid UnitId,
    Guid? TenantId = null) : IRequest;
