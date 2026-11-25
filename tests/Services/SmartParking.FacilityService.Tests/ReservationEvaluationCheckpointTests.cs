using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.FacilityService.Tests;

public sealed class ReservationEvaluationCheckpointTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 11, 10, 0, 0, TimeSpan.Zero);
    private static ReservationRow Booking(decimal amount = 20000)
    {
        var booking = ReservationRow.PrepareQuote(Guid.NewGuid(), Guid.NewGuid(), "51A12345", Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), "PAYOS", Start, Start.AddHours(1), Start.AddHours(1), Start.AddMinutes(-5),
            Start, amount, 0, amount, "VND", "{}", Guid.NewGuid(), "rq1-test", Start, "tariff", "coverage", false);
        if (amount > 0)
        {
            Assert.True(booking.BeginOrderProvisioning(booking.CreatedAt, TimeSpan.FromSeconds(1)));
            booking.RecordOrderCreated(Guid.NewGuid());
            Assert.True(booking.BeginPaidConfirmation(Guid.NewGuid(), booking.CreatedAt));
        }
        else
        {
            Assert.True(booking.BeginZeroPriceAcceptance(booking.CreatedAt, TimeSpan.FromSeconds(1)));
            booking.RecordZeroOrder(Guid.NewGuid(), Guid.NewGuid(), booking.CreatedAt);
        }
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
            Assert.Throws<InvalidOperationException>(() => booking.ConfirmAtEvaluation(booking.CollectionId, evaluated));
            Assert.Null(booking.ConfirmedAt);
            Assert.Null(booking.ConfirmationEffectId);
            return;
        }
        booking.ConfirmAtEvaluation(booking.CollectionId, evaluated);
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
        abandoned.ConfirmAtEvaluation(abandoned.CollectionId, Start.AddSeconds(-1));
        // Represents a fresh aggregate loaded after rollback; deployment tests prove actual SQL rollback.
        var fresh = Booking();
        Assert.Throws<InvalidOperationException>(() => fresh.ConfirmAtEvaluation(fresh.CollectionId, Start));
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
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExactHoldDeadlineClosesNormalConfirmationAndOnlyVerifiedPaidCanReacquire(bool covered)
    {
        var h = Start.AddMinutes(-2);
        var row = ReservationRow.PrepareQuote(Guid.NewGuid(), Guid.NewGuid(), "51A12345", Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), "PAYOS", Start, Start.AddHours(1), Start.AddHours(1),
            Start.AddMinutes(-5), h, 100, covered ? 100 : 0, covered ? 0 : 100, "VND", "{}", Guid.NewGuid(),
            "quote-1", Start, "tariff-1", "coverage-1", covered);
        Guid? collection = null;
        if (covered)
        {
            row.BeginZeroPriceAcceptance(row.CreatedAt, TimeSpan.FromSeconds(1));
            Assert.Throws<InvalidOperationException>(() => row.RecordZeroOrder(Guid.NewGuid(), Guid.Empty, row.CreatedAt));
            row.RecordZeroOrder(Guid.NewGuid(), Guid.NewGuid(), row.CreatedAt);
        }
        else
        {
            row.BeginOrderProvisioning(row.CreatedAt, TimeSpan.FromSeconds(1));
            row.RecordOrderCreated(Guid.NewGuid());
            collection = Guid.NewGuid();
            Assert.True(row.BeginPaidConfirmation(collection.Value, row.CreatedAt));
        }
        Assert.Throws<InvalidOperationException>(() => row.ConfirmAtEvaluation(collection, h));
        row.ReleaseExpiredHold(h);
        Assert.Null(row.ActiveClaimSlotId);
        if (covered)
        {
            Assert.Throws<InvalidOperationException>(() => row.ConfirmReacquiredPaidClaim(Guid.NewGuid(), h));
            Assert.Null(row.ConfirmedAt);
        }
        else
        {
            row.ConfirmReacquiredPaidClaim(collection!.Value, h);
            Assert.Equal(h, row.ConfirmedAt);
        }
    }
}
