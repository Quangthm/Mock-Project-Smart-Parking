using MediatR;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Reservations;

public sealed class ResolveReservationSlotHandler(IReservationSlotReader repository)
    : IRequestHandler<ResolveReservationSlotQuery, SelectedSlotMetadata>
{
    public async Task<SelectedSlotMetadata> Handle(ResolveReservationSlotQuery query, CancellationToken ct)
    {
        var slot = await repository.FindSlotMetadataAsync(query.SlotId, ct)
            ?? throw new StructureException("SLOT_NOT_FOUND", "The selected slot was not found.");
        if (slot.SlotId != query.SlotId || slot.SiteId == Guid.Empty || slot.TenantId == Guid.Empty)
            throw new StructureException("IDENTITY_UNAVAILABLE", "The selected slot scope could not be established.");
        return slot;
    }
}
