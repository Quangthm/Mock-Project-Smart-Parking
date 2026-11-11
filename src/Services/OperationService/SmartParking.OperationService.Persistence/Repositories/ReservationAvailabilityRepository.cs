using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Persistence.Repositories;

public sealed class ReservationAvailabilityRepository(ReservationDbContext db, OperationsConcurrency guards)
    : IReservationAvailabilityRepository
{
    public async Task LockAccountAsync(Guid accountId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Account guard requires a transaction.");
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({"reservation-account:" + accountId},0))", ct);
    }
    public Task<bool> HasUnclassifiedCancellationAsync(Guid accountId, CancellationToken ct) =>
        db.Reservations.AsNoTracking().AnyAsync(r => r.AccountId == accountId && r.Status == "CANCELLED" &&
            !db.Set<ReservationCancellationEvidence>().Any(e => e.ReservationId == r.Id && e.AccountId == accountId), ct);
    public async Task<IReadOnlyList<ReservationCancellationEvidence>> ReadCancellationsAsync(Guid accountId,
        DateTimeOffset dayStart, DateTimeOffset now, CancellationToken ct) =>
        await db.Set<ReservationCancellationEvidence>().AsNoTracking()
            .Where(e => e.AccountId == accountId && (e.CancelledAt >= dayStart || e.RestrictedUntil > now)).ToArrayAsync(ct);

    public async Task<ReservationAvailabilityFacts> ReadAsync(ReservationRow proposed, DateTimeOffset at, CancellationToken ct)
    {
        if (!guards.HoldsSite(proposed.SiteId) || !guards.HoldsPlate(proposed.NormalizedPlate))
            throw new InvalidOperationException("Availability reads require plate and site guards.");
        var rows = await db.Reservations.AsNoTracking().Where(r => r.TenantId == proposed.TenantId &&
            r.SiteId == proposed.SiteId &&
            (r.Status == "CONFIRMED" || r.Status == "ALLOCATED" || r.Status == "PARKING" ||
             r.Status == "PENDING_PAYMENT" && (r.HoldExpiresAt == null || r.HoldExpiresAt > at))).ToArrayAsync(ct);
        var sessions = await db.ParkingSessions.AsNoTracking().Where(s => s.TenantId == proposed.TenantId &&
            s.SiteId == proposed.SiteId && s.ExitTime == null).ToArrayAsync(ct);
        var allocations = await db.SlotAllocations.AsNoTracking().Where(a => a.TenantId == proposed.TenantId &&
            a.SiteId == proposed.SiteId && (a.AllocationStatus == "RESERVED" || a.AllocationStatus == "OCCUPIED")).ToArrayAsync(ct);
        // Do not silently sum two conflicting representations of capacity.
        var quotas = await db.Set<OperationalCapacityConfig>().AsNoTracking().Where(q =>
            q.TenantId == proposed.TenantId && q.SiteId == proposed.SiteId && q.VehicleType == proposed.VehicleType).ToArrayAsync(ct);
        return new(rows, sessions, allocations, quotas);
    }
}
