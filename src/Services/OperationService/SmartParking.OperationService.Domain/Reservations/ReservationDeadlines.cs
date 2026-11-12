namespace SmartParking.OperationService.Domain.Reservations;

/// <summary>
/// Arithmetic on accepted effective policies, not a default/override resolver or complete eligibility check.
/// </summary>
public sealed record ReservationDeadlines
{
    public DateTimeOffset Start { get; }
    public DateTimeOffset End { get; }
    public DateTimeOffset AllocationAt { get; }
    public DateTimeOffset FacilityFailureAt { get; }
    public DateTimeOffset PaymentDueAt { get; }

    public ReservationDeadlines(ReservationInterval interval, DateTimeOffset holdCreatedAt,
        TimeSpan allocationLead, TimeSpan facilityFailureLead, TimeSpan paymentHold)
    {
        ArgumentNullException.ThrowIfNull(interval);
        if (allocationLead < TimeSpan.Zero || facilityFailureLead < TimeSpan.Zero || paymentHold <= TimeSpan.Zero)
            throw new ArgumentException("Effective reservation durations are invalid.");
        if (holdCreatedAt >= interval.Start)
            throw new ArgumentException("A new payment hold must precede reservation start.");

        Start = interval.Start;
        End = interval.End;
        AllocationAt = Start - allocationLead;
        FacilityFailureAt = Start - facilityFailureLead;
        PaymentDueAt = paymentHold >= Start - holdCreatedAt
            ? Start : (holdCreatedAt + paymentHold).ToUniversalTime();
    }

    public bool AllocationIsDue(DateTimeOffset now) => now >= AllocationAt;
    public bool PaymentHoldHasExpired(DateTimeOffset now) => now >= PaymentDueAt;

    // Time precondition only; final eligibility also requires payment/capacity/guard checks.
    // The approved checkpoint time, not commit completion time, must precede Start.
    public bool MayAttemptConfirmation(DateTimeOffset now) => now < Start;
}
