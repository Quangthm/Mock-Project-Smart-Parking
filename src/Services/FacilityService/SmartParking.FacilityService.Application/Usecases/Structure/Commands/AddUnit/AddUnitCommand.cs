using MediatR;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.AddUnit;

public sealed record AddUnitCommand(
    Guid SiteId,
    UnitType Type,
    string Name,
    Guid? ParentId = null,
    int Capacity = 0,
    Guid? TenantId = null) : IRequest<Guid>;
