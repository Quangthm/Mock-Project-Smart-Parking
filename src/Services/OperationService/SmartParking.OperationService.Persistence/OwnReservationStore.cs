using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Application;
using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Persistence;

public sealed class OwnReservationStore(ReservationDbContext db) : IOwnReservationStore
{
    public async Task<IReadOnlyList<OwnReservation>> List(Guid account, DateTimeOffset? from, DateTimeOffset? to,
        int limit, ReservationCursor? after, DateTimeOffset asOf, CancellationToken ct)
    {
        var query = db.Reservations.AsNoTracking().Where(r => r.AccountId == account);
        if (from is not null && to is not null)
            query = query.Where(r => r.ExpectedStartTime < to && r.ExpectedEndTime > from);
        if (after is not null)
            query = query.Where(r => r.ExpectedStartTime > after.Start || r.ExpectedStartTime == after.Start && r.Id.CompareTo(after.Id) > 0);
        return (await query.OrderBy(r => r.ExpectedStartTime).ThenBy(r => r.Id).Take(limit).ToArrayAsync(ct)).Select(r => Map(r, asOf)).ToArray();
    }
    public async Task<OwnReservation?> Get(Guid account, Guid id, DateTimeOffset asOf, CancellationToken ct)
    {
        var row = await db.Reservations.AsNoTracking().SingleOrDefaultAsync(r => r.AccountId == account && r.Id == id, ct);
        return row is null ? null : Map(row, asOf);
    }
    private static OwnReservation Map(ReservationRow row, DateTimeOffset asOf) => new(row.Id, row.SiteId,
        new(row.VehicleType, row.OriginalPlate ?? row.NormalizedPlate ?? row.VehicleType),
        row.ExpectedStartTime, row.ExpectedEndTime, row.Status, asOf);
}
