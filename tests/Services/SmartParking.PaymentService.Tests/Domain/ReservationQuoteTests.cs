using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using Xunit;

namespace SmartParking.PaymentService.Tests.Domain;

public class ReservationQuoteTests
{
    [Fact]
    public void IsExpired_ReturnsTrue_WhenNowIsAfterValidUntil()
    {
        var quote = new ReservationQuote
        {
            ValidUntil = DateTimeOffset.UtcNow.AddMinutes(-1),
            Status = ReservationQuoteStatus.ISSUED
        };

        Assert.True(quote.IsExpired(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void IsExpired_ReturnsFalse_WhenNowIsBeforeValidUntil()
    {
        var quote = new ReservationQuote
        {
            ValidUntil = DateTimeOffset.UtcNow.AddMinutes(10),
            Status = ReservationQuoteStatus.ISSUED
        };

        Assert.False(quote.IsExpired(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void MarkAccepted_TransitionsStatusToAccepted()
    {
        var quote = new ReservationQuote
        {
            ValidUntil = DateTimeOffset.UtcNow.AddMinutes(10),
            Status = ReservationQuoteStatus.ISSUED
        };

        quote.MarkAccepted();

        Assert.Equal(ReservationQuoteStatus.ACCEPTED, quote.Status);
    }

    [Fact]
    public void MarkAccepted_ThrowsException_WhenStatusIsNotIssued()
    {
        var quote = new ReservationQuote
        {
            ValidUntil = DateTimeOffset.UtcNow.AddMinutes(10),
            Status = ReservationQuoteStatus.ACCEPTED
        };

        Assert.Throws<InvalidOperationException>(() => quote.MarkAccepted());
    }
}
