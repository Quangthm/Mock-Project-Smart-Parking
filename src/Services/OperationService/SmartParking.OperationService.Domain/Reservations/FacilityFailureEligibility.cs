namespace SmartParking.OperationService.Domain.Reservations;

public enum FacilityFailureReason
{
    UnfulfilledAtDeadline,
    UnfulfilledAfterLateConfirmation,
    UnfulfilledAfterAssignmentLoss
}

/// <summary>
/// Persist this independently of current allocation and payout state. Recovery has no revocation operation.
/// History/reconciliation and transaction serialization remain the workflow's responsibility.
/// </summary>
public sealed record FacilityFailureEligibility
{
    public DateTimeOffset EarnedAt { get; }
    public FacilityFailureReason Reason { get; }

    private FacilityFailureEligibility(DateTimeOffset earnedAt, FacilityFailureReason reason)
    {
        EarnedAt = earnedAt.ToUniversalTime();
        Reason = reason;
    }

    public static FacilityFailureEligibility Earn(ReservationDeadlines deadlines,
        DateTimeOffset effectiveAt, FacilityFailureReason reason)
    {
        ArgumentNullException.ThrowIfNull(deadlines);
        if (!Enum.IsDefined(reason))
            throw new ArgumentOutOfRangeException(nameof(reason));
        if (effectiveAt < deadlines.FacilityFailureAt || effectiveAt >= deadlines.Start)
            throw new ArgumentException("Eligibility must be earned at/after F and before T.");
        if (reason == FacilityFailureReason.UnfulfilledAtDeadline && effectiveAt != deadlines.FacilityFailureAt)
            throw new ArgumentException("Deadline eligibility retains the original F timestamp.");
        return new FacilityFailureEligibility(effectiveAt, reason);
    }

    // Actual accepted entry, not gate arrival, ends the right to exercise.
    public bool CanExercise(DateTimeOffset now, bool actualEntryAccepted, bool unusedBookingClosed) =>
        now >= EarnedAt && !actualEntryAccepted && !unusedBookingClosed;
}
