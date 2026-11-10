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
    IReadOnlyList<SlotVisualStateDto> Slots);

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
