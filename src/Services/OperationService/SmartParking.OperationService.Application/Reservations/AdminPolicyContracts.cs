using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record PolicyBoundDto(int Minimum, int Maximum);

public sealed record PolicyCatalogueItemDto(string Key, string Name, string Description, string DataType,
    string Unit, int DefaultValue, bool IsOverridableByOwner, PolicyBoundDto? Bound);

public sealed record PlatformPolicyCatalogueDto(Guid RevisionId, int Version, DateTimeOffset EffectiveFrom,
    string CancellationTimezone, IReadOnlyList<PolicyCatalogueItemDto> Policies);

public sealed record EffectivePolicyValueDto(int Value, string Unit, bool Inherited, string Source,
    bool IsZeroNotDisabled);

public sealed record ArrivalLateToleranceEffectiveDto(bool? Enabled, int Minutes, bool Inherited);

public sealed record EffectiveReservationPolicyDto(string Scope, Guid? TenantId, Guid? SiteId,
    DateTimeOffset EvaluatedAt, PolicyRevisionReference PlatformRevision, PolicyRevisionReference? SiteRevision,
    IReadOnlyDictionary<string, EffectivePolicyValueDto> Values, ArrivalLateToleranceEffectiveDto ArrivalLateTolerance,
    string CancellationTimezone);

public sealed record PolicyRevisionListItemDto
{
    public Guid Id { get; init; }
    public string Scope { get; init; } = string.Empty;
    public Guid? TenantId { get; init; }
    public Guid? SiteId { get; init; }
    public int Version { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTimeOffset EffectiveFrom { get; init; }
    public DateTimeOffset? EffectiveUntil { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public int? PaymentHoldDurationMinutes { get; init; }
    public int? MinReservationDurationMinutes { get; init; }
    public int? MaxReservationDurationHours { get; init; }
    public int? MaxAdvanceBookingDays { get; init; }
    public int? InterBookingBufferMinutes { get; init; }
    public int? ReservationProtectionWindowHours { get; init; }
    public int? AllocationLeadTimeMinutes { get; init; }
    public int? FacilityFailureRefundLeadMinutes { get; init; }
    public int? OccupancyValidityTimespanHours { get; init; }
    public int? ArrivalLateToleranceMinutes { get; init; }
    public bool? ArrivalLateToleranceEnabled { get; init; }
    public int? RapidCancellationCount { get; init; }
    public int? RapidCancellationWindowMinutes { get; init; }
    public int? RapidCancellationRestrictionMinutes { get; init; }
    public int? DailyCancellationLimit { get; init; }
    public string? CancellationTimezone { get; init; }
    public IReadOnlyDictionary<string, PolicyBoundDto> OverrideBounds { get; init; } =
        new Dictionary<string, PolicyBoundDto>();
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize, int TotalPages);

public sealed record CreatePolicyDraftRequestDto(
    Guid? TenantId,
    Guid? SiteId,
    int PaymentHoldDurationMinutes,
    int MinReservationDurationMinutes,
    int MaxReservationDurationHours,
    int MaxAdvanceBookingDays,
    int InterBookingBufferMinutes,
    int ReservationProtectionWindowHours,
    int AllocationLeadTimeMinutes,
    int FacilityFailureRefundLeadMinutes,
    int OccupancyValidityTimespanHours,
    int ArrivalLateToleranceMinutes,
    int RapidCancellationCount,
    int RapidCancellationWindowMinutes,
    int RapidCancellationRestrictionMinutes,
    int DailyCancellationLimit,
    bool? ArrivalLateToleranceEnabled,
    string CancellationTimezone,
    IReadOnlyDictionary<string, PolicyBoundDto>? OverrideBounds);

public sealed record UpdatePolicyDraftRequestDto(
    Guid? TenantId,
    Guid? SiteId,
    int PaymentHoldDurationMinutes,
    int MinReservationDurationMinutes,
    int MaxReservationDurationHours,
    int MaxAdvanceBookingDays,
    int InterBookingBufferMinutes,
    int ReservationProtectionWindowHours,
    int AllocationLeadTimeMinutes,
    int FacilityFailureRefundLeadMinutes,
    int OccupancyValidityTimespanHours,
    int ArrivalLateToleranceMinutes,
    int RapidCancellationCount,
    int RapidCancellationWindowMinutes,
    int RapidCancellationRestrictionMinutes,
    int DailyCancellationLimit,
    bool? ArrivalLateToleranceEnabled,
    string CancellationTimezone,
    IReadOnlyDictionary<string, PolicyBoundDto>? OverrideBounds,
    DateTimeOffset? ExpectedUpdatedAt);

public sealed record SchedulePolicyRevisionRequestDto(DateTimeOffset? EffectiveFrom);

public sealed record CreatePolicyDraftResultDto(Guid Id, int Version, string Scope, string Status, bool IsActive,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record UpdatePolicyDraftResultDto(Guid Id, int Version, string Scope, string Status, bool IsActive,
    DateTimeOffset UpdatedAt);

public sealed record PolicyActivationResultDto(Guid Id, int Version, string Scope, string Status, bool IsActive,
    DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveUntil, SupersededPolicyReferenceDto? SupersededRevision);

public sealed record SupersededPolicyReferenceDto(Guid Id, int Version, DateTimeOffset EffectiveUntil);

internal static class AdminPolicyWriteMapping
{
    public static void ApplyPlatformValues(OperationalPolicyRow row, int paymentHoldDurationMinutes,
        int minReservationDurationMinutes, int maxReservationDurationHours, int maxAdvanceBookingDays,
        int interBookingBufferMinutes, int reservationProtectionWindowHours, int allocationLeadTimeMinutes,
        int facilityFailureRefundLeadMinutes, int occupancyValidityTimespanHours, int arrivalLateToleranceMinutes,
        int rapidCancellationCount, int rapidCancellationWindowMinutes, int rapidCancellationRestrictionMinutes,
        int dailyCancellationLimit, string cancellationTimezone,
        IReadOnlyDictionary<string, PolicyBoundDto>? overrideBounds)
    {
        row.PaymentHoldDurationMinutes = paymentHoldDurationMinutes;
        row.MinReservationDurationMinutes = minReservationDurationMinutes;
        row.MaxReservationDurationHours = maxReservationDurationHours;
        row.MaxAdvanceBookingDays = maxAdvanceBookingDays;
        row.InterBookingBufferMinutes = interBookingBufferMinutes;
        row.ReservationProtectionWindowHours = reservationProtectionWindowHours;
        row.AllocationLeadTimeMinutes = allocationLeadTimeMinutes;
        row.FacilityFailureRefundLeadMinutes = facilityFailureRefundLeadMinutes;
        row.OccupancyValidityTimespanHours = occupancyValidityTimespanHours;
        row.ArrivalLateToleranceMinutes = arrivalLateToleranceMinutes;
        row.RapidCancellationCount = rapidCancellationCount;
        row.RapidCancellationWindowMinutes = rapidCancellationWindowMinutes;
        row.RapidCancellationRestrictionMinutes = rapidCancellationRestrictionMinutes;
        row.DailyCancellationLimit = dailyCancellationLimit;
        row.CancellationTimezone = cancellationTimezone;
        row.OverrideBounds = SerializeBounds(overrideBounds);
    }

    public static string? SerializeBounds(IReadOnlyDictionary<string, PolicyBoundDto>? bounds)
    {
        if (bounds is null) return null;
        var domain = bounds.ToDictionary(pair => pair.Key, pair => new PolicyBound(pair.Value.Minimum, pair.Value.Maximum));
        return System.Text.Json.JsonSerializer.Serialize(domain,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
    }
}