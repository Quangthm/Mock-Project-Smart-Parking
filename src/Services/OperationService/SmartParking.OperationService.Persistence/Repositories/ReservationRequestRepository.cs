using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Persistence.Repositories;

public sealed class ReservationRequestRepository(ReservationDbContext db) : IReservationRequestRepository
{
    public async Task LockAdmissionAsync(Guid accountId, Guid key, CancellationToken ct)
    {
        RequireTransaction();
        var resource = $"reservation-admission:{accountId:D}:{key:D}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({resource}, 0))", ct);
    }

    public Task<ReservationRequest?> FindByKeyAsync(Guid accountId, Guid key, CancellationToken ct) =>
        db.ReservationRequests.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == accountId &&
            x.Operation == "CREATE_RESERVATION" && x.IdempotencyKey == key, ct);

    public Task<ReservationRequest?> FindOwnAsync(Guid accountId, Guid id, CancellationToken ct) =>
        db.ReservationRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.AccountId == accountId, ct);

    public Task<ReservationRequest?> FindForWorkerAsync(Guid id, CancellationToken ct) =>
        db.ReservationRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task<ReservationRequest?> LockAsync(Guid id, CancellationToken ct)
    {
        RequireTransaction();
        var row = await db.ReservationRequests.FromSqlInterpolated(
            $"SELECT * FROM reservation_requests WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (row is not null) await db.Entry(row).ReloadAsync(ct);
        return row;
    }

    public async Task<IReadOnlyList<Guid>> DueAsync(DateTimeOffset now, CancellationToken ct) =>
        await db.ReservationRequests.AsNoTracking()
            .Where(x => (x.State == "CHECKING" || x.State == "EVALUATING") &&
                (x.NextActionAt <= now || x.State == "EVALUATING" && x.OverallDeadline <= now))
            .OrderBy(x => x.NextActionAt).ThenBy(x => x.Id).Select(x => x.Id).Take(100).ToListAsync(ct);

    public Task<Guid?> FindOwnByOrderAsync(Guid accountId, Guid orderId, CancellationToken ct) =>
        (from booking in db.Reservations.AsNoTracking()
         join request in db.ReservationRequests on booking.RequestId equals (Guid?)request.Id
         where booking.PaymentOrderId == orderId && booking.AccountId == accountId && request.AccountId == accountId
         select booking.RequestId).SingleOrDefaultAsync(ct);

    public void Add(ReservationRequest request) => db.ReservationRequests.Add(request);
    private void RequireTransaction()
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Admission guards require a UoW transaction.");
    }
}
