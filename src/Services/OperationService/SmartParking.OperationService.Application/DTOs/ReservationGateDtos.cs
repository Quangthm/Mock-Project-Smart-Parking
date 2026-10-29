namespace SmartParking.OperationService.Application.DTOs;

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
