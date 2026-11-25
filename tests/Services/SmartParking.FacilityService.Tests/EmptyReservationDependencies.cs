using SmartParking.FacilityService.Application.Reservations;
using SmartParking.FacilityService.Domain.Entities;

namespace SmartParking.FacilityService.Tests;

// Existing structure workflow fixtures have no reverse reservation dependencies.
internal sealed class EmptyReservationDependencies : IReservationSlotReader
{
    public SelectedSlotMetadata? Slot { get; init; }
    public Task<SelectedSlotMetadata?> FindSlotMetadataAsync(Guid slotId, CancellationToken ct) =>
        Task.FromResult(Slot?.SlotId == slotId ? Slot : null);
    public Task<ReservationSlotEligibility?> CheckSlotEligibilityAsync(Guid slotId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct) =>
        throw new NotSupportedException();
}
