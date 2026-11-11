using SmartParking.OperationService.Application.DTOs;

namespace SmartParking.OperationService.Application.Common.Interfaces.Persistence;

public interface IReservationGateStore
{
    Task<IReadOnlyList<ReservationGateEntry>> ListAsync(Guid tenantId, Guid siteId, CancellationToken ct);
    Task<IReadOnlyList<ParkingSessionHistoryEntry>> ListSessionsAsync(Guid tenantId, Guid siteId, CancellationToken ct);
    Task<ParkingSessionScanTarget?> FindScanTargetAsync(Guid tenantId, Guid siteId, Guid referenceId, CancellationToken ct);
    Task<ParkingSessionHistoryEntry> RecordScannedSlotAsync(Guid tenantId, Guid siteId, Guid sessionId, Guid slotId, CancellationToken ct);
    Task<ReservationGateEntry?> FindAsync(Guid tenantId, Guid siteId, string query, CancellationToken ct);
    Task<WalkInParkingSession?> FindActiveWalkInAsync(Guid tenantId, Guid siteId, string plate, CancellationToken ct);
    Task<WalkInParkingSession> CreateWalkInAsync(Guid tenantId, Guid siteId, Guid sessionId, string vehicleType, string plate, string? imageData, CancellationToken ct);
    Task<WalkInParkingSession> CheckOutWalkInAsync(Guid tenantId, Guid siteId, Guid sessionId, string plate, string? imageData, CancellationToken ct);
    Task<ReservationGateEntry> CheckInAsync(Guid tenantId, Guid siteId, Guid actorId, Guid reservationId, string? recognizedPlate, string? imageData, CancellationToken ct);
    Task<ReservationGateEntry> CheckOutAsync(Guid tenantId, Guid siteId, Guid actorId, Guid reservationId, string? recognizedPlate, string? imageData, CancellationToken ct);
    Task MarkWalkInPaymentPendingAsync(Guid tenantId, Guid siteId, Guid sessionId, CancellationToken ct);
    Task MarkReservationPaymentPendingAsync(Guid tenantId, Guid siteId, Guid reservationId, CancellationToken ct);
    Task<IReadOnlyList<Guid>> CompletePaymentAsync(Guid tenantId, Guid siteId, string referenceType, Guid referenceId, DateTimeOffset completedAt, CancellationToken ct);
}

public sealed record ParkingSessionScanTarget(
    Guid SessionId,
    Guid? ReservationId,
    Guid? CurrentSlotId,
    string VehicleType,
    bool IsScanned);
