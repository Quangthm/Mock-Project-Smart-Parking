using MediatR;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.RenameUnit;

public sealed record RenameUnitCommand(
    Guid SiteId,
    Guid UnitId,
    string Name,
    Guid? TenantId = null) : IRequest;
