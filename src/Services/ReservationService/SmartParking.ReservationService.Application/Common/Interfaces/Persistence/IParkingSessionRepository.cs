using SmartParking.ReservationService.Domain.Entities;

namespace SmartParking.ReservationService.Application.Common.Interfaces.Persistence;

public interface IParkingSessionRepository : IRepository<ParkingSessionRow>
{
    Task<ParkingSessionRow?> FindForUpdateAsync(Guid tenantId, Guid siteId, Guid sessionId, CancellationToken cancellationToken);
}
