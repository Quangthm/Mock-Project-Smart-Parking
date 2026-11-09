using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.FacilityService.Tests;

public sealed class ReservationFirstSliceRuleTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 9, 10, 0, 0, TimeSpan.Zero);
    private static ReservationInterval Interval => new(Start, Start.AddHours(1));
    private static ReservationDeadlines Deadlines(DateTimeOffset created) =>
        new(Interval, created, TimeSpan.FromHours(1), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(5));

    [Fact]
    public void AdjacentIntervalsNeedOnlyTheEffectiveGap()
    {
        var next = new ReservationInterval(Interval.End, Interval.End.AddHours(1));
        Assert.False(Interval.ConflictsWith(next, TimeSpan.Zero));
        Assert.True(Interval.ConflictsWith(next, TimeSpan.FromTicks(1)));
        Assert.True(next.ConflictsWith(Interval, TimeSpan.FromTicks(1)));
        var separated = new ReservationInterval(Interval.End.AddMinutes(5), Interval.End.AddHours(1));
        Assert.False(Interval.ConflictsWith(separated, TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void OverlapIsIndependentOfLotOrAccount()
    {
        var overlap = new ReservationInterval(Start.AddMinutes(-15), Start.AddMinutes(15));
        Assert.True(Interval.ConflictsWith(overlap, TimeSpan.Zero));
        Assert.True(overlap.ConflictsWith(Interval, TimeSpan.Zero));
    }

    [Fact]
    public void EffectiveDurationAndHorizonAreInclusiveButStartMustBeFuture()
    {
        var minimum = TimeSpan.FromMinutes(15);
        var maximum = TimeSpan.FromHours(24);
        var horizon = TimeSpan.FromDays(7);
        Assert.True(new ReservationInterval(Start, Start + minimum)
            .MeetsBookingWindow(Start - horizon, minimum, maximum, horizon));
        Assert.True(new ReservationInterval(Start, Start + maximum)
            .MeetsBookingWindow(Start.AddMinutes(-1), minimum, maximum, horizon));
        Assert.False(Interval.MeetsBookingWindow(Start, minimum, maximum, horizon));
        Assert.False(Interval.MeetsBookingWindow(Start - horizon - TimeSpan.FromTicks(1), minimum, maximum, horizon));
        Assert.Throws<ArgumentException>(() => new ReservationInterval(Start, Start));
        Assert.Throws<ArgumentOutOfRangeException>(() => Interval.ConflictsWith(Interval, TimeSpan.FromTicks(-1)));
    }

    [Fact]
    public void LateHoldIsCappedAtStartAndEqualityIsExpired()
    {
        var deadlines = Deadlines(Start.AddMinutes(-2));
        Assert.Equal(Start, deadlines.PaymentDueAt);
        Assert.False(deadlines.PaymentHoldHasExpired(Start.AddTicks(-1)));
        Assert.True(deadlines.PaymentHoldHasExpired(Start));
        Assert.False(deadlines.MayAttemptConfirmation(Start));
        Assert.False(deadlines.MayAttemptConfirmation(Start.AddSeconds(2)));
    }

    [Fact]
    public void EarlierExpiredHoldMayOnlyAttemptReacquisitionBeforeStart()
    {
        var deadlines = Deadlines(Start.AddMinutes(-10));
        Assert.Equal(Start.AddMinutes(-5), deadlines.PaymentDueAt);
        Assert.True(deadlines.PaymentHoldHasExpired(Start.AddMinutes(-4)));
        Assert.True(deadlines.MayAttemptConfirmation(Start.AddMinutes(-4)));
        Assert.False(deadlines.MayAttemptConfirmation(Start));
        // This precondition deliberately proves neither capacity nor commit-before-T.
    }

    [Fact]
    public void AllocationAndRefundDeadlinesUseIndependentEffectivePolicies()
    {
        var deadlines = new ReservationDeadlines(Interval, Start.AddHours(-2),
            TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(3));
        Assert.Equal(Start.AddMinutes(-30), deadlines.AllocationAt);
        Assert.Equal(Start.AddMinutes(-10), deadlines.FacilityFailureAt);
        Assert.True(deadlines.AllocationIsDue(deadlines.AllocationAt));
        Assert.True(deadlines.AllocationIsDue(Start.AddMinutes(-20)));
    }

    [Fact]
    public void RecoveryAndArrivalDoNotEndEarnedRightButEntryAndClosureDo()
    {
        var eligibility = FacilityFailureEligibility.Earn(Deadlines(Start.AddHours(-2)),
            Start.AddMinutes(-15), FacilityFailureReason.UnfulfilledAtDeadline);
        Assert.True(eligibility.CanExercise(Start.AddMinutes(-13), false, false));
        Assert.True(eligibility.CanExercise(Start, false, false));
        Assert.False(eligibility.CanExercise(Start, true, false));
        Assert.False(eligibility.CanExercise(Start, false, true));
        Assert.False(eligibility.CanExercise(Start.AddMinutes(-16), false, false));
        Assert.Equal(Start.AddMinutes(-15), eligibility.EarnedAt);
    }

    [Theory]
    [InlineData(FacilityFailureReason.UnfulfilledAfterLateConfirmation)]
    [InlineData(FacilityFailureReason.UnfulfilledAfterAssignmentLoss)]
    public void FailedImmediateRecoveryCanEarnRightBetweenFAndT(FacilityFailureReason reason)
    {
        var deadlines = Deadlines(Start.AddHours(-2));
        var right = FacilityFailureEligibility.Earn(deadlines, Start.AddMinutes(-10), reason);
        Assert.Equal(Start.AddMinutes(-10), right.EarnedAt);
        Assert.Throws<ArgumentException>(() => FacilityFailureEligibility.Earn(deadlines, Start, reason));
    }

    [Fact]
    public void DowntimeCannotRewriteDeadlineEligibilityAsRecoveryTime()
    {
        var deadlines = Deadlines(Start.AddHours(-2));
        Assert.Throws<ArgumentException>(() => FacilityFailureEligibility.Earn(deadlines,
            Start.AddMinutes(-13), FacilityFailureReason.UnfulfilledAtDeadline));
        Assert.Throws<ArgumentException>(() => FacilityFailureEligibility.Earn(deadlines,
            Start.AddMinutes(-16), FacilityFailureReason.UnfulfilledAfterAssignmentLoss));
    }
}
