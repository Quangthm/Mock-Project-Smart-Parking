using MediatR;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.ConfigureSlot;

public sealed record ConfigureSlotCommand(
    Guid SiteId,
    Guid SlotId,
    VehicleType? VehicleType,
    SlotType SlotType,
    string? Coordinates3D = null,
    string? Features = null,
    IReadOnlyList<VehicleType>? VehicleTypes = null,
    VehicleType? PrimaryVehicleType = null,
    Guid? TenantId = null) : IRequest;
