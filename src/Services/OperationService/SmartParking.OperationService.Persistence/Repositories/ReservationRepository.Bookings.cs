using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Persistence.Repositories;

public sealed partial class ReservationRepository
{
    public Task<string?> FindZeroDispositionReasonAsync(Guid reservationId, Guid accountId, CancellationToken ct) =>
        db.Set<SmartParking.OperationService.Domain.Reservations.ReservationCancellationEvidence>()
            .AsNoTracking().Where(e => e.ReservationId == reservationId && e.AccountId == accountId)
            .Select(e => e.Reason).SingleOrDefaultAsync(ct);

    public Task<bool> HasQuoteBindingAsync(Guid quoteId, CancellationToken ct) =>
        db.Reservations.AnyAsync(row => row.QuoteId == quoteId, ct);

    public async Task RequirePlateAvailableAsync(ReservationRow proposed, DateTimeOffset now, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null || string.IsNullOrWhiteSpace(proposed.NormalizedPlate))
            throw new InvalidOperationException("Plate acceptance requires a transaction and canonical identity.");
        var plate = proposed.NormalizedPlate;
        if (await EffectiveCommitments(db, proposed.SiteId, proposed.ExpectedStartTime,
            proposed.ClaimEndTime ?? proposed.ExpectedEndTime, now).AnyAsync(r =>
                r.Id != proposed.Id && r.NormalizedPlate == plate, ct))
            throw new ReservationGateException(409, "VEHICLE_TIME_CONFLICT", "This plate already has an overlapping reservation at this site.");
    }

    internal static IQueryable<ReservationRow> EffectiveCommitments(ReservationDbContext db, Guid siteId,
        DateTimeOffset start, DateTimeOffset? end, DateTimeOffset now, Guid[]? allocationOwners = null)
    {
        var upperBound = end ?? start;
        return db.Reservations.AsNoTracking().Where(r => r.SiteId == siteId &&
            ((end.HasValue ? r.ExpectedStartTime < upperBound : r.ExpectedStartTime <= upperBound) &&
                (r.ClaimEndTime ?? r.ExpectedEndTime) > start ||
             allocationOwners != null && allocationOwners.Contains(r.Id)) &&
            (r.Status == "CONFIRMED" || r.Status == "ALLOCATED" || r.Status == "PARKING" ||
             r.Status == "PENDING_PAYMENT" && (r.HoldExpiresAt == null || r.HoldExpiresAt > now)));
    }

    public async Task<BookingReadResult?> FindOwnBookingAsync(Guid accountId, Guid id, CancellationToken ct)
    {
        var reservation = await db.Reservations.AsNoTracking()
            .SingleOrDefaultAsync(r => r.AccountId == accountId && r.Id == id && r.RequestId != null, ct);
        if (reservation is null)
            return null;

        var results = await ReadBookingLinksAsync(accountId, [reservation], ct);
        return results[0];
    }

    public async Task<IReadOnlyList<BookingReadResult>> ListOwnBookingsAsync(Guid accountId, int take,
        DateTimeOffset? before, Guid? beforeId, CancellationToken ct, DateTimeOffset? from = null, DateTimeOffset? to = null)
    {
        var query = db.Reservations.AsNoTracking().Where(r => r.AccountId == accountId && r.RequestId != null);
        if (from is { } rangeStart && to is { } rangeEnd)
            query = query.Where(r => r.ExpectedStartTime < rangeEnd && r.ExpectedEndTime > rangeStart);
        if (before is { } cursorTime && beforeId is { } cursorId)
        {
            query = query.Where(r => r.CreatedAt < cursorTime ||
                r.CreatedAt == cursorTime && r.Id.CompareTo(cursorId) < 0);
        }

        var reservations = await query.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
            .Take(take).ToListAsync(ct);
        return await ReadBookingLinksAsync(accountId, reservations, ct);
    }

    private async Task<IReadOnlyList<BookingReadResult>> ReadBookingLinksAsync(Guid accountId,
        IReadOnlyList<ReservationRow> reservations, CancellationToken ct)
    {
        if (reservations.Count == 0)
            return [];

        var ids = reservations.Select(r => r.Id).ToArray();
        var bookingsInPage = db.Reservations.AsNoTracking()
            .Where(r => ids.Contains(r.Id) && r.AccountId == accountId && r.RequestId != null);

        // Match the current allocation lookup, retaining the latest released row when no active row exists.
        var allocations = await (
            from booking in bookingsInPage
            join allocation in db.SlotAllocations.AsNoTracking()
                on new { ReservationId = (Guid?)booking.Id, booking.TenantId, booking.SiteId }
                equals new { allocation.ReservationId, allocation.TenantId, allocation.SiteId }
            orderby (allocation.AllocationStatus == "RESERVED" || allocation.AllocationStatus == "OCCUPIED") descending,
                allocation.CreatedAt descending, allocation.Id descending
            select new { ReservationId = booking.Id, allocation.SlotId, allocation.AllocationStatus }
        ).ToListAsync(ct);

        // Use the gate lookup's active-then-latest visit precedence, with an ID tie-break.
        var sessions = await (
            from booking in bookingsInPage
            join session in db.ParkingSessions.AsNoTracking()
                on new { ReservationId = (Guid?)booking.Id, booking.TenantId, booking.SiteId, AccountId = (Guid?)booking.AccountId }
                equals new { session.ReservationId, session.TenantId, session.SiteId, session.AccountId }
            orderby (session.ExitTime == null) descending, session.CreatedAt descending, session.Id descending
            select new { ReservationId = booking.Id, session.Id }
        ).ToListAsync(ct);

        var allocationsByBooking = allocations.ToLookup(a => a.ReservationId);
        var sessionsByBooking = sessions.ToLookup(s => s.ReservationId);
        return reservations.Select(reservation =>
        {
            var allocation = allocationsByBooking[reservation.Id].FirstOrDefault();
            var session = sessionsByBooking[reservation.Id].FirstOrDefault();
            return new BookingReadResult(reservation, allocation?.SlotId, allocation?.AllocationStatus, session?.Id);
        }).ToArray();
    }

    public Task<ReservationRow?> FindBookingAsync(Guid reservationId, CancellationToken ct) =>
        db.Reservations.AsNoTracking().SingleOrDefaultAsync(r => r.Id == reservationId && r.RequestId != null, ct);

    public Task<bool> OrderAssignedToAnotherReservationAsync(Guid orderId, Guid reservationId, CancellationToken ct) =>
        db.Reservations.AsNoTracking().AnyAsync(r => r.PaymentOrderId == orderId && r.Id != reservationId, ct);

    public async Task<DateTimeOffset> DatabaseNowAsync(CancellationToken ct)
    {
        var value = await ScalarAsync("SELECT clock_timestamp()", ct);
        return value is DateTimeOffset offset ? offset : new DateTimeOffset(DateTime.SpecifyKind((DateTime)value!, DateTimeKind.Utc));
    }

    public async Task RequireDurableConfirmationAsync(CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Confirmation requires the owning UoW transaction.");
        // Durability is still required; transaction completion is not the eligibility cutoff.
        await db.Database.ExecuteSqlRawAsync("SET LOCAL synchronous_commit = 'on'", ct);
    }

    private async Task<object?> ScalarAsync(string sql, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            var result = await command.ExecuteScalarAsync(ct);
            return result is DBNull ? null : result;
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }
}
