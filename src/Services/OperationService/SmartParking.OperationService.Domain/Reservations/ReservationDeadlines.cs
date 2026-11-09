namespace SmartParking.OperationService.Domain.Reservations;

/// <summary>
/// Arithmetic on accepted effective policies, not a default/override resolver or commit-time proof.
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

    // A true result is only a necessary precondition. It must never authorize confirmation:
    // the transaction may still cross Start before commit (D4c).
    public bool MayAttemptConfirmation(DateTimeOffset now) => now < Start;
}
