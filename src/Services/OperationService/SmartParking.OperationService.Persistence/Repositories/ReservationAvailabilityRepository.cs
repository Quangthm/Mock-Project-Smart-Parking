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
        var start = proposed.ExpectedStartTime;
        var end = proposed.ClaimEndTime ?? proposed.ExpectedEndTime;
        var sessions = await db.ParkingSessions.AsNoTracking().Where(s => s.TenantId == proposed.TenantId &&
            s.SiteId == proposed.SiteId && s.ExitTime == null).ToArrayAsync(ct);
        SlotAllocationRow[] allocations;
        ReservationRow[] rows;
        while (true)
        {
            allocations = await db.SlotAllocations.AsNoTracking().Where(a => a.TenantId == proposed.TenantId &&
                a.SiteId == proposed.SiteId && a.AllocatedStartTime < end && a.AllocatedEndTime > start &&
                (a.AllocationStatus == "RESERVED" || a.AllocationStatus == "OCCUPIED")).ToArrayAsync(ct);
            var owners = allocations.Where(a => a.ReservationId.HasValue).Select(a => a.ReservationId!.Value).Distinct().ToArray();
            rows = await ReservationRepository.EffectiveCommitments(db, proposed.SiteId, start, end, at, owners)
                .Where(r => r.TenantId == proposed.TenantId).ToArrayAsync(ct);
            // Only a flexible booking carries a choice across a time boundary. A fixed blocker is a leaf.
            var flexible = rows.Where(r => r.ActiveClaimSlotId == null && r.TargetSlotId == null &&
                r.ExpectedStartTime < end && start < (r.ClaimEndTime ?? r.ExpectedEndTime)).ToArray();
            var lower = flexible.Select(r => r.ExpectedStartTime).Append(start).Min();
            var upper = flexible.Select(r => r.ClaimEndTime ?? r.ExpectedEndTime).Append(end).Max();
            if (lower == start && upper == end) break;
            start = lower;
            end = upper;
        }
        // Do not silently sum two conflicting representations of capacity.
        var quotas = await db.Set<OperationalCapacityConfig>().AsNoTracking().Where(q =>
            q.TenantId == proposed.TenantId && q.SiteId == proposed.SiteId && q.VehicleType == proposed.VehicleType).ToArrayAsync(ct);
        return new(rows, sessions, allocations, quotas);
    }
}
