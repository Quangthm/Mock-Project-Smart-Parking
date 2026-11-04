using MediatR;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.AddSlot;

public sealed record AddSlotCommand(
    Guid SiteId,
    Guid UnitId,
    string Code,
    VehicleType? VehicleType,
    SlotType Type = SlotType.STANDARD,
    IReadOnlyList<VehicleType>? VehicleTypes = null,
    VehicleType? PrimaryVehicleType = null,
    Guid? TenantId = null) : IRequest<Guid>;
