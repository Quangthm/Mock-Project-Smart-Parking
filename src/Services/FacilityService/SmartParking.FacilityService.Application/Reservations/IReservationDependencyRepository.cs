using SmartParking.FacilityService.Domain.Entities;

namespace SmartParking.FacilityService.Application.Reservations;

public interface IReservationDependencyRepository
{
    Task<SelectedSlotMetadata?> FindSlotMetadataAsync(Guid slotId, CancellationToken ct);
    Task<ReservationSlotEligibility?> CheckSlotEligibilityAsync(Guid slotId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct);
    Task LockAsync(Guid token, Guid siteId, CancellationToken ct);
    Task<ReservationDependency?> FindAsync(Guid token, CancellationToken ct);
    Task<bool> HasHeldAsync(Guid siteId, CancellationToken ct);
    Task<bool> HasPendingStructureEditAsync(Guid siteId, CancellationToken ct);
    Task<ResourceSnapshot> SnapshotAsync(Guid siteId, Guid slotId, CancellationToken ct);
    Task<DateTimeOffset> NowAsync(CancellationToken ct);
    void Add(ReservationDependency dependency);
}
