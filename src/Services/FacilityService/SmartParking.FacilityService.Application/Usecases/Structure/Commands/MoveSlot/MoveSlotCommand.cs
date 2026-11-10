using MediatR;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.MoveSlot;

public sealed record MoveSlotCommand(
    Guid SiteId,
    Guid SlotId,
    Guid TargetUnitId,
    string Code,
    Guid? TenantId = null) : IRequest;
