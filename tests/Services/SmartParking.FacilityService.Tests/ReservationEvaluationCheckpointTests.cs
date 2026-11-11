using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.FacilityService.Tests;

public sealed class ReservationEvaluationCheckpointTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 11, 10, 0, 0, TimeSpan.Zero);
    private static ReservationRow Booking(decimal amount = 20000) => ReservationRow.Prepare(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "v1", "PAYOS", Start,
        Start.AddHours(1), Start.AddHours(1), Start.AddMinutes(-5), Start, amount, amount, "VND", "{}", "{}");

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    public void EligibilityUsesCheckpointStrictlyBeforeStart(int offset, bool eligible)
    {
        var booking = Booking();
        var evaluated = Start.AddSeconds(offset);
        if (!eligible)
        {
            Assert.Throws<InvalidOperationException>(() => booking.ConfirmAtEvaluation(Guid.NewGuid(), evaluated));
            Assert.Null(booking.ConfirmedAt);
            Assert.Null(booking.ConfirmationEffectId);
            return;
        }
        booking.ConfirmAtEvaluation(Guid.NewGuid(), evaluated);
        Assert.Equal("CONFIRMED", booking.Status);
        Assert.Equal(evaluated, booking.ConfirmationEvaluatedAt);
        Assert.Equal(evaluated, booking.ConfirmedAt);
        var effect = booking.ConfirmationEffectId;
        booking.ReleaseExpiredHold(Start.AddSeconds(2));
        Assert.Equal("CONFIRMED", booking.Status);
        Assert.Equal(effect, booking.ConfirmationEffectId);
    }

    [Fact]
    public void FreshEvaluationAtStartRejectsAfterAnAbandonedAttempt()
    {
        var abandoned = Booking();
        abandoned.ConfirmAtEvaluation(Guid.NewGuid(), Start.AddSeconds(-1));
        // Represents a fresh aggregate loaded after rollback; deployment tests prove actual SQL rollback.
        var fresh = Booking();
        Assert.Throws<InvalidOperationException>(() => fresh.ConfirmAtEvaluation(Guid.NewGuid(), Start));
        Assert.Null(fresh.ConfirmationEvaluatedAt);
    }

    [Fact]
    public void GenuineZeroPriceRequiresNoCollectionAndStillUsesCutoff()
    {
        var booking = Booking(0);
        Assert.Throws<InvalidOperationException>(() => booking.ConfirmAtEvaluation(Guid.NewGuid(), Start.AddSeconds(-1)));
        booking.ConfirmAtEvaluation(null, Start.AddSeconds(-1));
        Assert.Equal("CONFIRMED", booking.Status);
        Assert.Null(booking.CollectionId);
        Assert.Throws<InvalidOperationException>(() => booking.ConfirmAtEvaluation(null, Start.AddSeconds(1)));
    }
}
