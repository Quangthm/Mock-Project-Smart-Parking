using MediatR;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.RemoveUnit;

public sealed record RemoveUnitCommand(
    Guid SiteId,
    Guid UnitId,
    Guid? TenantId = null) : IRequest;
