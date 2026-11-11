namespace SmartParking.OperationService.Application.Usecase.ParkingSessions.Queries.GetCurrentVehicleLocation;

public sealed record VehicleLocationResponseDto(
    Guid SessionId,
    Guid SiteId,
    string SiteCode,
    string? AllocationStatus, // "RESERVED" | "OCCUPIED"
    RecordedLocationDto RecordedLocation,
    DateTimeOffset? RecordedAt,
    FreshnessInfoDto Freshness);

public sealed record RecordedLocationDto(
    string Precision, // "SLOT" | "ZONE" | "UNKNOWN"
    Guid? SlotId,
    string? SlotCode,
    Guid? ZoneId,
    string? ZoneCode,
    string? FloorCode);

public sealed record FreshnessInfoDto(
    DateTimeOffset GeneratedAt,
    bool IsStale,
    int StalenessThresholdSeconds,
    string Source);