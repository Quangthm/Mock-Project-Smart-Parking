using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Application.Discovery;

namespace SmartParking.OperationService.Persistence.Repositories;

public sealed class HistoricalBookingSiteReader(ReservationDbContext db) : IHistoricalBookingSiteReader
{
    public Task<HistoricalBookingSite?> LatestConfirmedAsync(Guid accountId, CancellationToken ct) =>
        db.Reservations.AsNoTracking()
            .Where(row => row.AccountId == accountId && row.ConfirmedAt != null &&
                (row.Status == "CONFIRMED" || row.Status == "ALLOCATED" ||
                 row.Status == "PARKING" || row.Status == "COMPLETED"))
            .OrderByDescending(row => row.ConfirmedAt).ThenByDescending(row => row.Id)
            .Select(row => new HistoricalBookingSite(row.SiteId, row.ConfirmedAt!.Value))
            .FirstOrDefaultAsync(ct);
}
