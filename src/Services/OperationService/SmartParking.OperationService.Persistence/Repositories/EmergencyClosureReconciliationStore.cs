using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Persistence.Repositories;

// Emergency-closure mirror and reconciliation reads. Decision reads are untracked; rows the
// handler mutates are loaded tracked so a single SaveChanges commits them with the booking state.
public sealed class EmergencyClosureReconciliationStore(ReservationDbContext db, TimeProvider clock)
    : IEmergencyClosureReconciliationStore
{
    public Task<bool> HasProcessedEventAsync(Guid eventId, CancellationToken ct) =>
        db.FacilityEventReceipts.AsNoTracking().AnyAsync(receipt => receipt.EventId == eventId, ct);

    public async Task UpsertEmergencyClosureAsync(StructureEmergencyClosureRow closure, CancellationToken ct)
    {
        // A redelivered restriction must not reopen a closure that was already released.
        var exists = await db.StructureEmergencyClosures
            .AnyAsync(row => row.RestrictionId == closure.RestrictionId, ct);
        if (exists) return;

        db.StructureEmergencyClosures.Add(closure);
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkClosureReleasedAsync(
        Guid restrictionId, Guid tenantId, Guid siteId, DateTimeOffset releasedAt, CancellationToken ct)
    {
        var closure = await db.StructureEmergencyClosures.FirstOrDefaultAsync(row =>
            row.RestrictionId == restrictionId && row.TenantId == tenantId && row.SiteId == siteId, ct);
        if (closure is null || closure.ReleasedAt is not null) return;

        closure.ReleasedAt = releasedAt;
        await db.SaveChangesAsync(ct);
    }

    public async Task RecordProcessedEventAsync(FacilityEventReceiptRow receipt, CancellationToken ct)
    {
        var exists = await db.FacilityEventReceipts
            .AnyAsync(row => row.EventId == receipt.EventId, ct);
        if (exists) return;

        db.FacilityEventReceipts.Add(receipt);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ReservationRow>> FindEmergencyCandidatesAsync(
        Guid tenantId, Guid siteId, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveUntil,
        CancellationToken ct) =>
        await db.Reservations.AsNoTracking()
            .Where(row => row.TenantId == tenantId && row.SiteId == siteId)
            .Where(row =>
                (row.Status == "CONFIRMED" || row.Status == "ALLOCATED") &&
                    (row.ClaimEndTime ?? row.ExpectedEndTime) > effectiveFrom &&
                    (effectiveUntil == null || row.ExpectedStartTime < effectiveUntil)
                || row.Status == "PENDING_PAYMENT" &&
                    (row.HoldExpiresAt == null || row.HoldExpiresAt > effectiveFrom)
                || row.Status == "CANCELLED" && row.ConfirmationState == "RECONCILIATION_REQUIRED")
            .OrderBy(row => row.ExpectedStartTime)
            .ThenBy(row => row.ConfirmedAt)
            .ThenBy(row => row.Id)
            .ToArrayAsync(ct);

    public async Task<IReadOnlyList<SlotAllocationRow>> FindActiveAllocationsAsync(
        Guid tenantId, Guid siteId, Guid reservationId, CancellationToken ct) =>
        // Only a reservation (never an occupied bay) may be withdrawn: an OCCUPIED allocation means
        // a vehicle is physically standing there, so it must never be released or moved.
        await db.SlotAllocations
            .Where(row => row.TenantId == tenantId && row.SiteId == siteId &&
                row.ReservationId == reservationId &&
                row.AllocationStatus == "RESERVED")
            .ToArrayAsync(ct);

    public async Task<IReadOnlyList<ReservationRow>> FindSiteActiveReservationsForReallocationAsync(
        Guid tenantId, Guid siteId, DateTimeOffset from, DateTimeOffset until, CancellationToken ct)
    {
        // An unexpired pending hold is a real commitment for the requested window, so it blocks a
        // reallocation exactly like a confirmed booking does; an expired hold does not.
        var now = clock.GetUtcNow();
        return await db.Reservations.AsNoTracking()
            .Where(row => row.TenantId == tenantId && row.SiteId == siteId &&
                ((row.Status == "CONFIRMED" || row.Status == "ALLOCATED" || row.Status == "PARKING") ||
                 row.Status == "PENDING_PAYMENT" && (row.HoldExpiresAt == null || row.HoldExpiresAt > now)) &&
                row.ExpectedStartTime < until &&
                (row.ClaimEndTime ?? row.ExpectedEndTime) > from)
            .ToArrayAsync(ct);
    }

    public async Task<IReadOnlyList<ParkingSessionRow>> FindSiteOpenSessionsAsync(
        Guid tenantId, Guid siteId, CancellationToken ct) =>
        await db.ParkingSessions.AsNoTracking()
            .Where(row => row.TenantId == tenantId && row.SiteId == siteId && row.ExitTime == null)
            .ToArrayAsync(ct);

    public async Task<IReadOnlyList<StructureMaintenanceClosureRow>> FindActiveMaintenanceClosuresAsync(
        Guid tenantId, Guid siteId, CancellationToken ct) =>
        await db.StructureMaintenanceClosures.AsNoTracking()
            .Where(row => row.TenantId == tenantId && row.SiteId == siteId && row.CancelledAt == null)
            .ToArrayAsync(ct);

    public async Task<IReadOnlyList<StructureEmergencyClosureRow>> FindActiveEmergencyClosuresAsync(
        Guid tenantId, Guid siteId, CancellationToken ct) =>
        await db.StructureEmergencyClosures.AsNoTracking()
            .Where(row => row.TenantId == tenantId && row.SiteId == siteId && row.ReleasedAt == null)
            .ToArrayAsync(ct);

    public Task<bool> HasCancellationEvidenceAsync(Guid reservationId, CancellationToken ct) =>
        db.Set<ReservationCancellationEvidence>().AsNoTracking()
            .AnyAsync(evidence => evidence.ReservationId == reservationId, ct);

    public void AddCancellationEvidence(ReservationCancellationEvidence evidence) =>
        db.Set<ReservationCancellationEvidence>().Add(evidence);

    public async Task DecrementCapacityPoolAsync(
        Guid tenantId, Guid siteId, string vehicleType, string previousStatus, Guid? spatialUnitId,
        CancellationToken ct)
    {
        var pool = await db.SiteCapacityPools.FirstOrDefaultAsync(row =>
            row.TenantId == tenantId && row.SiteId == siteId &&
            row.VehicleType == vehicleType && row.SpatialUnitId == spatialUnitId, ct);
        if (pool is null) return;

        if (previousStatus == "PENDING_PAYMENT")
            pool.PendingPaymentCount = Math.Max(0, pool.PendingPaymentCount - 1);
        else if (previousStatus is "CONFIRMED" or "ALLOCATED")
            pool.CurrentReservedCount = Math.Max(0, pool.CurrentReservedCount - 1);
        else
            return;

        pool.UpdatedAt = clock.GetUtcNow();
    }
}