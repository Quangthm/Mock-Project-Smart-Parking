using MediatR;

namespace SmartParking.FacilityService.Application.Usecases.Gate.Commands.ClaimGateSlot;

public sealed record ClaimGateSlotCommand(
    Guid TenantId,
    Guid SiteId,
    string VehicleType,
    Guid? PreferredSlotId = null,
    Guid? TargetUnitId = null,
    bool AllowReservedSlot = false) : IRequest<OperatorGateSlot>;
