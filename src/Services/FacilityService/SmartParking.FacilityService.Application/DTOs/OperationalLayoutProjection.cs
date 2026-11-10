using System.Text.Json;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.DTOs;

// Wire contract for GET /api/parking-lots/{id}/operational-layout.
// Source of truth: docs/03-design/api/3d-digital-twin-openapi.yaml (v1.2.0).
public static class OperationalLayoutContract
{
    public const string Version = "1.2.0";
    public const int StalenessThresholdSeconds = 30;
}

public enum PhysicalState { AVAILABLE, OCCUPIED, UNKNOWN, MAINTENANCE, UNAVAILABLE }
public enum ReservationState { UNRESERVED, RESERVED, PROTECTED, BACKUP }
public enum SessionType { STANDARD, EV_CHARGING, RESERVED, VIP }

public sealed record OperationalLayoutProjectionDto(
    Guid SiteId,
    string SiteCode,
    string SiteName,
    string ContractVersion,
    DateTimeOffset GeneratedAt,
    bool IsFresh,
    int StalenessThresholdSeconds,
    LayoutMetricsDto Metrics,
    IReadOnlyList<FloorProjectionDto> Floors,
    IReadOnlyList<SlotVisualStateDto> Slots,
    // v1.3.0 additions: omitted from the v1.2.0 wire shape while null so the current
    // contract envelope is preserved until the version is bumped.
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    DateTimeOffset? TelemetryHeartbeatAt = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    DriverReservationDto? DriverReservation = null);

public sealed record DriverReservationDto(
    Guid ReservationId,
    bool IsReallocated,
    PositionScopeDto RequestedPosition,
    AllocatedPositionDto? AllocatedPosition);

public sealed record PositionScopeDto(
    string Scope, // "SLOT" | "ZONE"
    Guid? SlotId,
    Guid? ZoneId);

public sealed record AllocatedPositionDto(
    string Scope, // "SLOT" | "ZONE"
    Guid? SlotId,
    Guid AllocationId,
    string AllocationStatus); // "RESERVED" | "OCCUPIED"

public sealed record LayoutMetricsDto(
    int TotalPhysicalCapacity,
    int OccupiedCount,
    int ReservedCount,
    int AvailableCarSlots,
    MotorcycleCapacityDto MotorcycleCapacity,
    IReadOnlyList<ZoneCapacityDto> ZoneCapacities);

public sealed record MotorcycleCapacityDto(
    int MaxCapacity,
    int OccupiedCount,
    int AvailableCount);

public sealed record ZoneCapacityDto(
    Guid ZoneId,
    string ZoneCode,
    string FloorCode,
    string? Name,
    int MaxCapacity,
    int OccupiedCount,
    int AllocatedSlotCount,
    int ZoneOnlyReservedCount);

public sealed record FloorProjectionDto(
    Guid FloorId,
    string FloorCode,
    string Name,
    int Level,
    int MaxCapacity);

public sealed record SlotVisualStateDto(
    Guid SlotId,
    string SlotCode,
    Guid FloorId,
    string FloorCode,
    string? ZoneCode,
    string? RowCode,
    VehicleType SupportedVehicleType,
    SlotType SlotType,
    PhysicalState PhysicalState,
    ReservationState ReservationState,
    SessionType? SessionType,
    bool IsEvCharging,
    bool IsBookable,
    [property: System.Text.Json.Serialization.JsonPropertyName("coordinates3d")] JsonElement? Coordinates3D,
    JsonElement? Features,
    JsonElement? OperatorContext,
    DateTimeOffset UpdatedAt);
