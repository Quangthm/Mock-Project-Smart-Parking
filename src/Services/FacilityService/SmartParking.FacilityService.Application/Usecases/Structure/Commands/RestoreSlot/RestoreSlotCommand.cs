using MediatR;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.RestoreSlot;

public sealed record RestoreSlotCommand(
    Guid SiteId,
    Guid SlotId,
    Guid? TenantId = null) : IRequest;
