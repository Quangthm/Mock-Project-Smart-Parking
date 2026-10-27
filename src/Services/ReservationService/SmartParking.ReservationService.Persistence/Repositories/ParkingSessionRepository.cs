using Microsoft.EntityFrameworkCore;
using SmartParking.ReservationService.Application.Common.Interfaces.Persistence;
using SmartParking.ReservationService.Domain.Entities;

namespace SmartParking.ReservationService.Persistence.Repositories;

public sealed class ParkingSessionRepository(ReservationDbContext db)
    : GenericRepository<ParkingSessionRow>(db), IParkingSessionRepository
{
    public Task<ParkingSessionRow?> FindForUpdateAsync(
        Guid tenantId, Guid siteId, Guid sessionId, CancellationToken cancellationToken) =>
        Set.SingleOrDefaultAsync(s => s.Id == sessionId &&
            s.TenantId == tenantId && s.SiteId == siteId, cancellationToken);
}
