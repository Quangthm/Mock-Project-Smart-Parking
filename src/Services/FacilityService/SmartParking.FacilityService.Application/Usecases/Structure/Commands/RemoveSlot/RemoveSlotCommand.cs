using MediatR;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.RemoveSlot;

public sealed record RemoveSlotCommand(
    Guid SiteId,
    Guid SlotId,
    Guid? TenantId = null) : IRequest;
