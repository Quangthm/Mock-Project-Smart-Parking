using SmartParking.OperationService.Application.DTOs;
using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Application.Common.Interfaces.Persistence;

public interface IParkingSessionRepository : IRepository<ParkingSessionRow>
{
    Task<ParkingSessionRow?> FindForUpdateAsync(Guid tenantId, Guid siteId, Guid sessionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ParkingSessionHistoryEntry>> ListSessionsAsync(Guid tenantId, Guid siteId, CancellationToken ct);
    Task<WalkInParkingSession?> FindActiveWalkInAsync(Guid tenantId, Guid siteId, string plate, CancellationToken ct);
    Task<bool> HasActivePlateAsync(Guid tenantId, Guid siteId, string plate, CancellationToken ct);
    Task<ParkingSessionRow?> FindByReservationAsync(Guid tenantId, Guid siteId, Guid reservationId, bool active, CancellationToken ct);
}
