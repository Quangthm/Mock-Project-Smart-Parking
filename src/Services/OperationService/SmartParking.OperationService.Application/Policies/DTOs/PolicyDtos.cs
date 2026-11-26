using System.Text.Json.Serialization;
using SmartParking.OperationService.Domain.Policies;

namespace SmartParking.OperationService.Application.Policies.DTOs;

public sealed record PolicyCatalogueResponse(
    [property: JsonPropertyName("siteId")] Guid SiteId,
    [property: JsonPropertyName("activeRevision")] int ActiveRevision,
    [property: JsonPropertyName("activeSince")] DateTimeOffset ActiveSince,
    [property: JsonPropertyName("hasDraft")] bool HasDraft,
    [property: JsonPropertyName("policies")] IReadOnlyList<PolicyCatalogueItem> Policies
);

public sealed record PolicyDraftResponse(
    [property: JsonPropertyName("siteId")] Guid SiteId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("targetRevision")] int TargetRevision,
    [property: JsonPropertyName("updatedAt")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("scheduledActivationAt")] DateTimeOffset? ScheduledActivationAt,
    [property: JsonPropertyName("policies")] PolicyDraftConfig Policies
);

public sealed class SavePolicyDraftRequest
{
    [JsonPropertyName("policies")]
    public PolicyDraftConfig? Policies { get; set; }

    [JsonPropertyName("scheduledActivationAt")]
    public DateTimeOffset? ScheduledActivationAt { get; set; }

    // Flat compatibility properties
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

    public PolicyDraftConfig ToDraftConfig()
    {
        if (Policies != null) return Policies;

        return new PolicyDraftConfig
        {
            PaymentHoldDurationMinutes = PaymentHoldDurationMinutes,
            ArrivalLateToleranceMinutes = ArrivalLateToleranceMinutes,
            ArrivalLateToleranceEnabled = ArrivalLateToleranceEnabled,
            MinReservationDurationMinutes = MinReservationDurationMinutes,
            MaxReservationDurationHours = MaxReservationDurationHours,
            ReservationProtectionWindowHours = ReservationProtectionWindowHours,
            AllocationLeadTimeMinutes = AllocationLeadTimeMinutes,
            OccupancyValidityTimespanHours = OccupancyValidityTimespanHours,
            MaxAdvanceBookingDays = MaxAdvanceBookingDays,
            InterBookingBufferMinutes = InterBookingBufferMinutes,
            DriverReallocationRequestLimit = DriverReallocationRequestLimit,
            FacilityFailureRefundLeadMinutes = FacilityFailureRefundLeadMinutes,
            ParkingProductEnabled = ParkingProductEnabled
        };
    }
}

public sealed record SavePolicyDraftResponse(
    [property: JsonPropertyName("siteId")] Guid SiteId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("targetRevision")] int TargetRevision,
    [property: JsonPropertyName("updatedAt")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("message")] string Message
);

public sealed class PublishPolicyRequest
{
    private string _mode = "IMMEDIATE";

    [JsonPropertyName("activationMode")]
    public string ActivationMode
    {
        get => _mode;
        set => _mode = value;
    }

    [JsonPropertyName("mode")]
    public string Mode
    {
        get => _mode;
        set => _mode = value;
    }

    private DateTimeOffset? _scheduledAt;

    [JsonPropertyName("scheduledActivationAt")]
    public DateTimeOffset? ScheduledActivationAt
    {
        get => _scheduledAt;
        set => _scheduledAt = value;
    }

    [JsonPropertyName("scheduledAt")]
    public DateTimeOffset? ScheduledAt
    {
        get => _scheduledAt;
        set => _scheduledAt = value;
    }

    [JsonPropertyName("publishNote")]
    public string? PublishNote { get; set; }
}

public sealed record PublishPolicyResponse(
    [property: JsonPropertyName("siteId")] Guid SiteId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("newRevision")] int NewRevision,
    [property: JsonPropertyName("activatedAt")] DateTimeOffset ActivatedAt,
    [property: JsonPropertyName("publishNote")] string? PublishNote,
    [property: JsonIgnore] bool IsReplayed = false
);

public sealed record PolicyRevisionsResponse(
    [property: JsonPropertyName("siteId")] Guid SiteId,
    [property: JsonPropertyName("totalRevisions")] int TotalRevisions,
    [property: JsonPropertyName("items")] IReadOnlyList<PolicyRevisionSummary> Items
);

public sealed record PolicySnapshotResponse(
    [property: JsonPropertyName("siteId")] Guid SiteId,
    [property: JsonPropertyName("revisionNumber")] int RevisionNumber,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("effectiveFrom")] DateTimeOffset EffectiveFrom,
    [property: JsonPropertyName("effectiveTo")] DateTimeOffset? EffectiveTo,
    [property: JsonPropertyName("publishedBy")] Guid PublishedBy,
    [property: JsonPropertyName("policies")] Dictionary<string, object?> Policies
);
