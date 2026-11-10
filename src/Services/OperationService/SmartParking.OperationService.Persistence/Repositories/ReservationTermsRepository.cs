using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Persistence.Repositories;

public sealed class ReservationTermsRepository(ReservationDbContext db) : IReservationTermsRepository
{
    public void Add(ReservationTerms terms) => db.Set<ReservationTerms>().Add(terms);
    public async Task<ReservationTerms?> LockAsync(Guid id, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Terms acceptance requires a UoW transaction.");
        var row = await db.Set<ReservationTerms>().FromSqlInterpolated(
            $"SELECT * FROM reservation_terms WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (row is not null) await db.Entry(row).ReloadAsync(ct);
        return row;
    }
}
