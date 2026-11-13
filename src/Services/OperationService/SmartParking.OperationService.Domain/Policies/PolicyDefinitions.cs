using System.Text.Json.Serialization;

namespace SmartParking.OperationService.Domain.Policies;

public enum PolicyCategory
{
    ReservationWindow,
    CheckinTolerance,
    ProductSubscription,
    AdvanceBooking
}

public sealed record AdminBounds(
    [property: JsonPropertyName("min")] int Min,
    [property: JsonPropertyName("max")] int Max
);

public sealed record PolicyCatalogueItem(
    string Key,
    string Category,
    string DataType,
    string Unit,
    object? AdminDefault,
    AdminBounds? AdminBounds,
    object? ConfiguredValue,
    object? EffectiveValue,
    string Source,
    bool IsConfigurable
);

public sealed record PolicyDraftEntity(
    Guid Id,
    Guid TenantId,
    Guid SiteId,
    int TargetRevision,
    string PayloadJson,
    DateTimeOffset? ScheduledActivationAt,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy
);

public sealed record PolicyRevisionSummary(
    int RevisionNumber,
    string Status,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    Guid PublishedBy,
    string? PublishNote
);

public sealed record PolicySnapshot(
    Guid SiteId,
    int RevisionNumber,
    string Status,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    Guid PublishedBy,
    Dictionary<string, object?> Policies
);

public sealed class PolicyDraftConfig
{
    [JsonPropertyName("paymentHoldDurationMinutes")]
    public int? PaymentHoldDurationMinutes { get; set; }

    [JsonPropertyName("arrivalLateToleranceMinutes")]
    public int? ArrivalLateToleranceMinutes { get; set; }

    [JsonPropertyName("arrivalLateToleranceEnabled")]
    public bool? ArrivalLateToleranceEnabled { get; set; }

    [JsonPropertyName("minReservationDurationMinutes")]
    public int? MinReservationDurationMinutes { get; set; }

    [JsonPropertyName("maxReservationDurationHours")]
    public int? MaxReservationDurationHours { get; set; }

    [JsonPropertyName("reservationProtectionWindowHours")]
    public int? ReservationProtectionWindowHours { get; set; }

    [JsonPropertyName("allocationLeadTimeMinutes")]
    public int? AllocationLeadTimeMinutes { get; set; }

    [JsonPropertyName("occupancyValidityTimespanHours")]
    public int? OccupancyValidityTimespanHours { get; set; }

    [JsonPropertyName("maxAdvanceBookingDays")]
    public int? MaxAdvanceBookingDays { get; set; }

    [JsonPropertyName("interBookingBufferMinutes")]
    public int? InterBookingBufferMinutes { get; set; }

    [JsonPropertyName("driverReallocationRequestLimit")]
    public int? DriverReallocationRequestLimit { get; set; }

    [JsonPropertyName("facilityFailureRefundLeadMinutes")]
    public int? FacilityFailureRefundLeadMinutes { get; set; }

    [JsonPropertyName("parkingProductEnabled")]
    public ParkingProductFlags? ParkingProductEnabled { get; set; }
}

public sealed class ParkingProductFlags
{
    [JsonPropertyName("monthly")]
    public bool Monthly { get; set; }

    [JsonPropertyName("yearly")]
    public bool Yearly { get; set; }
}
