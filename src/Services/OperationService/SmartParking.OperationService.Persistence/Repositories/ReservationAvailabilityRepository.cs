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

    public Task<ReservationAvailabilityFacts> ReadAsync(ReservationRow proposed, DateTimeOffset at, CancellationToken ct)
    {
        RequireGuards(proposed.SiteId, proposed.NormalizedPlate);
        return new CapacityFactsQuery(db).ReadAsync(proposed.TenantId, proposed.SiteId, proposed.ExpectedStartTime,
            proposed.ClaimEndTime ?? proposed.ExpectedEndTime, at, proposed.VehicleType, ct);
    }

    public Task<ReservationAvailabilityFacts> ReadPhysicalAsync(ParkingSessionRow session, DateTimeOffset at, CancellationToken ct)
    {
        RequireGuards(session.SiteId, session.NormalizedPlate);
        return new CapacityFactsQuery(db).ReadAsync(session.TenantId, session.SiteId, at, null, at, null, ct);
    }

    private void RequireGuards(Guid siteId, string? plate)
    {
        if (!guards.HoldsSite(siteId) || !guards.HoldsPlate(plate))
            throw new InvalidOperationException("Availability reads require plate and site guards.");
    }
}
