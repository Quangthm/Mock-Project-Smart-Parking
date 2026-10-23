using SmartParking.ReservationService.Domain;

namespace SmartParking.ReservationService.Application;

public sealed record ReservationGateEntry(
    Guid Id,
    Guid AccountId,
    Guid SiteId,
    string SiteName,
    Guid? SlotId,
    string SlotNumber,
    string LicensePlate,
    string Status,
    string? PaymentStatus,
    string? PaymentMethod,
    decimal? Amount,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    DateTimeOffset? EntryTime,
    DateTimeOffset? ExitTime,
    string? EntryImageUrl,
    string? ExitImageUrl,
    DateTimeOffset CreatedAt);

public sealed record WalkInParkingSession(
    Guid Id,
    Guid SiteId,
    Guid? SlotId,
    string? SlotNumber,
    string LicensePlate,
    string VehicleType,
    DateTimeOffset EntryTime,
    DateTimeOffset? ExitTime,
    string? EntryImageUrl,
    string? ExitImageUrl);

public sealed record ParkingSessionHistoryEntry(
    Guid Id,
    Guid? ReservationId,
    Guid TenantId,
    Guid SiteId,
    Guid? AccessItemId,
    Guid? AccountId,
    Guid? CurrentSlotId,
    string? SlotNumber,
    string? OriginalPlate,
    string? NormalizedPlate,
    string VehicleType,
    string? TicketReference,
    DateTimeOffset EntryTime,
    string? EntryImageUrl,
    DateTimeOffset? ExitTime,
    string? ExitImageUrl,
    string Status,
    int Version,
    DateTimeOffset CreatedAt);

public sealed class ReservationGateException(int statusCode, string code, string message)
    : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}

public sealed record OperatorSiteScope(Guid UserId, Guid TenantId, Guid SiteId);

public interface IOperatorIdentityClient
{
    Task<OperatorSiteScope> RequireSiteAsync(Guid siteId, CancellationToken ct);
}

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
