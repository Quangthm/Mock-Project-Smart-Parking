using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Persistence.Repositories;

public sealed class ReservationRepository(ReservationDbContext db)
    : GenericRepository<ReservationRow>(db), IReservationRepository
{
    public Task<ReservationRow?> FindForUpdateAsync(
        Guid tenantId, Guid siteId, Guid reservationId, CancellationToken cancellationToken) =>
        Set.FromSqlInterpolated($"SELECT * FROM reservations WHERE id = {reservationId} AND tenant_id = {tenantId} AND site_id = {siteId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
}
