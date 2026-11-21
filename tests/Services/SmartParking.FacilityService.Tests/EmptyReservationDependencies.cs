using SmartParking.FacilityService.Application.Reservations;
using SmartParking.FacilityService.Domain.Entities;

namespace SmartParking.FacilityService.Tests;

// Existing structure workflow fixtures have no reverse reservation dependencies.
internal sealed class EmptyReservationDependencies : IReservationDependencyRepository
{
    public BookingFacts? Slot { get; init; }
    public Task<Guid?> FindSlotSiteAsync(Guid slotId, CancellationToken ct) =>
        Task.FromResult(Slot?.SlotId == slotId ? Slot.SiteId : (Guid?)null);
    public bool Held { get; set; }
    public Task<bool> HasHeldAsync(Guid siteId, CancellationToken ct) => Task.FromResult(Held);
    public Task LockAsync(Guid token, Guid siteId, CancellationToken ct) => throw new NotSupportedException();
    public Task<ReservationDependency?> FindAsync(Guid token, CancellationToken ct) => throw new NotSupportedException();
    public Task<bool> HasPendingStructureEditAsync(Guid siteId, CancellationToken ct) => throw new NotSupportedException();
    public Task<ResourceSnapshot> SnapshotAsync(Guid siteId, Guid slotId, CancellationToken ct) => throw new NotSupportedException();
    public Task<DateTimeOffset> NowAsync(CancellationToken ct) => throw new NotSupportedException();
    public void Add(ReservationDependency dependency) => throw new NotSupportedException();
}
