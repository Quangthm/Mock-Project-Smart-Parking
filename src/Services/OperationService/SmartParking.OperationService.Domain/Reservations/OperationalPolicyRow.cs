namespace SmartParking.OperationService.Domain.Reservations;

// Reservation policy revision. Policy management belongs to the catalogue owner, outside this task.
public sealed class OperationalPolicyRow
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public Guid? SiteId { get; set; }
    public int Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveUntil { get; set; }
    public bool IsActive { get; set; }
    public bool? ArrivalLateToleranceEnabled { get; set; }
    public int? ArrivalLateToleranceMinutes { get; set; }
    public int? PaymentHoldDurationMinutes { get; set; }
    public int? MinReservationDurationMinutes { get; set; }
    public int? MaxReservationDurationHours { get; set; }
    public int? MaxAdvanceBookingDays { get; set; }
    public int? InterBookingBufferMinutes { get; set; }
    public int? ReservationProtectionWindowHours { get; set; }
    public int? AllocationLeadTimeMinutes { get; set; }
    public int? FacilityFailureRefundLeadMinutes { get; set; }
    public int? OccupancyValidityTimespanHours { get; set; }
    public int? RapidCancellationCount { get; set; }
    public int? RapidCancellationWindowMinutes { get; set; }
    public int? RapidCancellationRestrictionMinutes { get; set; }
    public int? DailyCancellationLimit { get; set; }
    public string? OverrideBounds { get; set; }
    public string? CancellationTimezone { get; set; }
}

