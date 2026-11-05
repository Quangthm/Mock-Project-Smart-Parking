using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Persistence.Repositories;

public sealed class ParkingSessionRepository(ReservationDbContext db)
    : GenericRepository<ParkingSessionRow>(db), IParkingSessionRepository
{
    public Task<ParkingSessionRow?> FindForUpdateAsync(
        Guid tenantId, Guid siteId, Guid sessionId, CancellationToken cancellationToken) =>
        Set.SingleOrDefaultAsync(
            session => session.Id == sessionId &&
                session.TenantId == tenantId &&
                session.SiteId == siteId,
            cancellationToken);
}
