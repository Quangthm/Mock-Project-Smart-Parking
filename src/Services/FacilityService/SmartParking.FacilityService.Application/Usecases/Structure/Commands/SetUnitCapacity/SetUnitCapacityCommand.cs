using MediatR;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.SetUnitCapacity;

public sealed record SetUnitCapacityCommand(
    Guid SiteId,
    Guid UnitId,
    int Capacity,
    Guid? TenantId = null) : IRequest;
