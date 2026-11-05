using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Application.Common.Interfaces.Persistence;

public interface IParkingSessionRepository : IRepository<ParkingSessionRow>
{
    Task<ParkingSessionRow?> FindForUpdateAsync(Guid tenantId, Guid siteId, Guid sessionId, CancellationToken cancellationToken);
}
