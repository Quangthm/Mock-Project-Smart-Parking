using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Persistence.Repositories;

public sealed partial class ReservationRepository
{
    public async Task RequirePlateAvailableAsync(ReservationRow proposed, DateTimeOffset now, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null || string.IsNullOrWhiteSpace(proposed.NormalizedPlate))
            throw new InvalidOperationException("Plate acceptance requires a transaction and canonical identity.");
        var plate = proposed.NormalizedPlate;
        var claimEnd = proposed.ClaimEndTime ?? proposed.ExpectedEndTime;
        if (await db.ParkingSessions.AsNoTracking().AnyAsync(s => s.NormalizedPlate == plate && s.ExitTime == null &&
            s.ReservationId != proposed.Id, ct) || await db.Reservations.AsNoTracking().AnyAsync(r =>
            r.Id != proposed.Id && r.NormalizedPlate == plate &&
            (r.Status == "PARKING" ||
                r.Status == "PENDING_PAYMENT" && (r.HoldExpiresAt == null || r.HoldExpiresAt > now) ||
                r.Status == "CONFIRMED" || r.Status == "ALLOCATED") &&
            (r.Status == "PARKING" || r.ExpectedStartTime < claimEnd &&
                (r.ClaimEndTime ?? r.ExpectedEndTime) > proposed.ExpectedStartTime), ct))
            throw new ReservationGateException(409, "VEHICLE_TIME_CONFLICT", "This plate already has a conflicting booking or physical visit.");
    }

    public Task<ReservationRow?> FindOwnBookingAsync(Guid accountId, Guid id, CancellationToken ct) =>
        db.Reservations.AsNoTracking().SingleOrDefaultAsync(r => r.AccountId == accountId && r.Id == id && r.RequestId != null, ct);

    public async Task<IReadOnlyList<ReservationRow>> ListOwnBookingsAsync(Guid accountId, int take,
        DateTimeOffset? before, Guid? beforeId, CancellationToken ct, DateTimeOffset? from = null, DateTimeOffset? to = null) =>
        await db.Reservations.AsNoTracking().Where(r => r.AccountId == accountId && r.RequestId != null &&
            (from == null || r.ExpectedStartTime < to && r.ExpectedEndTime > from) &&
            (before == null || r.CreatedAt < before || r.CreatedAt == before && r.Id.CompareTo(beforeId!.Value) < 0))
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).Take(take).ToListAsync(ct);

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
