using SmartParking.OperationService.Application.DTOs;

namespace SmartParking.OperationService.Application.Common.Interfaces.Persistence;

public interface IReservationGateStore
{
    Task<IReadOnlyList<ReservationGateEntry>> ListAsync(Guid tenantId, Guid siteId, CancellationToken ct);
    Task<IReadOnlyList<ParkingSessionHistoryEntry>> ListSessionsAsync(Guid tenantId, Guid siteId, CancellationToken ct);
    Task<ReservationGateEntry?> FindAsync(Guid tenantId, Guid siteId, string query, CancellationToken ct);
    Task<WalkInParkingSession?> FindActiveWalkInAsync(Guid tenantId, Guid siteId, string plate, CancellationToken ct);
    Task<WalkInParkingSession> CreateWalkInAsync(Guid tenantId, Guid siteId, Guid sessionId, string vehicleType, string plate, string? imageData, CancellationToken ct);
    Task<WalkInParkingSession> CheckOutWalkInAsync(Guid tenantId, Guid siteId, Guid sessionId, string plate, string? imageData, CancellationToken ct);
    Task<ReservationGateEntry> CheckInAsync(Guid tenantId, Guid siteId, Guid actorId, Guid reservationId, string? recognizedPlate, string? imageData, CancellationToken ct);
    Task<ReservationGateEntry> CheckOutAsync(Guid tenantId, Guid siteId, Guid actorId, Guid reservationId, string? recognizedPlate, string? imageData, CancellationToken ct);
}
