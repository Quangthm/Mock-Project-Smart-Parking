using Microsoft.EntityFrameworkCore;
using SmartParking.ReservationService.Application.Common.Interfaces.Persistence;
using SmartParking.ReservationService.Domain.Entities;

namespace SmartParking.ReservationService.Persistence.Repositories;

public sealed class ParkingSessionRepository(ReservationDbContext db)
    : GenericRepository<ParkingSessionRow>(db), IParkingSessionRepository
{
    public Task<ParkingSessionRow?> FindForUpdateAsync(
        Guid tenantId, Guid siteId, Guid sessionId, CancellationToken cancellationToken) =>
        Set.FromSqlInterpolated($"SELECT * FROM parking_sessions WHERE id = {sessionId} AND tenant_id = {tenantId} AND site_id = {siteId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
}
