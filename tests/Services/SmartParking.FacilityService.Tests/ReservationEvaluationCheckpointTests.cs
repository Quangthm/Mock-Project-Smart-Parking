using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.FacilityService.Tests;

public sealed class ReservationEvaluationCheckpointTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 11, 10, 0, 0, TimeSpan.Zero);
    private static ReservationRow Booking(decimal amount = 20000)
    {
        var booking = ReservationRow.PrepareQuote(Guid.NewGuid(), Guid.NewGuid(), "51A12345", Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), "PAYOS", Start, Start.AddHours(1), Start.AddHours(1), Start.AddMinutes(-5),
            amount > 0 ? Start : null, amount, 0, amount, "VND", "{}", Guid.NewGuid(), Start, "tariff", "coverage", false);
        if (amount > 0) booking.BindCollection(Guid.NewGuid(), Guid.NewGuid(), "settled");
        return booking;
    }

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
            Assert.Throws<InvalidOperationException>(() => booking.ConfirmPaid(evaluated));
            Assert.Null(booking.ConfirmedAt);
            Assert.Null(booking.ConfirmationEffectId);
            return;
        }
        booking.ConfirmPaid(evaluated);
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
        abandoned.ConfirmPaid(Start.AddSeconds(-1));
        // Represents a fresh aggregate loaded after rollback; deployment tests prove actual SQL rollback.
        var fresh = Booking();
        Assert.Throws<InvalidOperationException>(() => fresh.ConfirmPaid(Start));
        Assert.Null(fresh.ConfirmationEvaluatedAt);
    }

    [Fact]
    public void GenuineZeroPriceRequiresNoCollectionAndStillUsesCutoff()
    {
        var booking = Booking(0);
        booking.ConfirmZeroAdmission(Start.AddSeconds(-1));
        Assert.Null(booking.CollectionId);
        Assert.Null(booking.HoldExpiresAt);
        booking.ReleaseExpiredHold(Start);
        Assert.Equal("CONFIRMED", booking.Status);
        Assert.Throws<InvalidOperationException>(() => booking.ConfirmZeroAdmission(Start));
    }
}
