using SmartParking.FacilityService.Domain.Entities;

namespace SmartParking.FacilityService.Application.Reservations;

public interface IReservationSlotReader
{
    Task<SelectedSlotMetadata?> FindSlotMetadataAsync(Guid slotId, CancellationToken ct);
    Task<ReservationSlotEligibility?> CheckSlotEligibilityAsync(Guid slotId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct);
}
