using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

// Persistence port for the emergency-closure mirror and the reconciliation it drives. Reads used
// for decisions are untracked; rows that will be mutated are loaded tracked and saved by the
// handler inside its own short transaction.
public interface IEmergencyClosureReconciliationStore
{
    Task<bool> HasProcessedEventAsync(Guid eventId, CancellationToken ct);

    Task UpsertEmergencyClosureAsync(StructureEmergencyClosureRow closure, CancellationToken ct);

    Task MarkClosureReleasedAsync(
        Guid restrictionId, Guid tenantId, Guid siteId, DateTimeOffset releasedAt, CancellationToken ct);

    Task RecordProcessedEventAsync(FacilityEventReceiptRow receipt, CancellationToken ct);

    Task<IReadOnlyList<ReservationRow>> FindEmergencyCandidatesAsync(
        Guid tenantId, Guid siteId, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveUntil,
        CancellationToken ct);

    Task<IReadOnlyList<SlotAllocationRow>> FindActiveAllocationsAsync(
        Guid tenantId, Guid siteId, Guid reservationId, CancellationToken ct);

    Task<IReadOnlyList<ReservationRow>> FindSiteActiveReservationsForReallocationAsync(
        Guid tenantId, Guid siteId, DateTimeOffset from, DateTimeOffset until, CancellationToken ct);

    Task<IReadOnlyList<ParkingSessionRow>> FindSiteOpenSessionsAsync(
        Guid tenantId, Guid siteId, CancellationToken ct);

    Task<IReadOnlyList<StructureMaintenanceClosureRow>> FindActiveMaintenanceClosuresAsync(
        Guid tenantId, Guid siteId, CancellationToken ct);

    Task<IReadOnlyList<StructureEmergencyClosureRow>> FindActiveEmergencyClosuresAsync(
        Guid tenantId, Guid siteId, CancellationToken ct);

    Task<bool> HasCancellationEvidenceAsync(Guid reservationId, CancellationToken ct);

    void AddCancellationEvidence(ReservationCancellationEvidence evidence);

    // Mutates the tracked pool row only; the caller's SaveChanges commits it with the cancellation.
    Task DecrementCapacityPoolAsync(
        Guid tenantId, Guid siteId, string vehicleType, string previousStatus, Guid? spatialUnitId,
        CancellationToken ct);
}