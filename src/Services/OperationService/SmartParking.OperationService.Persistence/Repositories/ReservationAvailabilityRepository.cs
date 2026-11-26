using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
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
        var allocations = await db.SlotAllocations.AsNoTracking().Where(a => a.TenantId == proposed.TenantId &&
            a.SiteId == proposed.SiteId && a.AllocatedStartTime < end && a.AllocatedEndTime > start &&
            (a.AllocationStatus == "RESERVED" || a.AllocationStatus == "OCCUPIED")).ToArrayAsync(ct);
        // An effective assignment can outlast its booking. Include its owner even when the booking window has ended.
        var owners = allocations.Where(a => a.ReservationId.HasValue).Select(a => a.ReservationId!.Value).Distinct().ToArray();
        var rows = await ReservationRepository.EffectiveCommitments(db, proposed.SiteId, start, end, at, owners)
            .Where(r => r.TenantId == proposed.TenantId).ToArrayAsync(ct);
        var quotas = await db.Set<ReservationQuotaConfig>().AsNoTracking().Where(q =>
            q.TenantId == proposed.TenantId && q.SiteId == proposed.SiteId && q.VehicleType == proposed.VehicleType && q.IsActive)
            .Select(q => q.MaxReservationQuota).Take(2).ToArrayAsync(ct);
        if (quotas.Length > 1)
            throw new ReservationGateException(503, "RESERVATION_QUOTA_CONFIGURATION_INVALID",
                "Multiple active reservation quotas apply to this site and vehicle category.");
        return new(rows, sessions, allocations, quotas.Length == 0 ? null : quotas[0]);
    }
}
