using MediatR;

namespace SmartParking.FacilityService.Application.Usecases.Gate.Commands.ReleaseGateSlot;

public sealed record ReleaseGateSlotCommand(
    Guid TenantId,
    Guid SiteId,
    Guid SlotId) : IRequest;
