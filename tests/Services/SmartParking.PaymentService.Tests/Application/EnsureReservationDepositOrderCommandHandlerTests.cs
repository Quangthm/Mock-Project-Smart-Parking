using SmartParking.Contracts.ReservationPayments;
using SmartParking.PaymentService.Application.Usecase.Reservations.Commands.EnsureReservationDepositOrder;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Exceptions;
using SmartParking.PaymentService.Tests.TestDoubles;

namespace SmartParking.PaymentService.Tests.Application;

public sealed class EnsureReservationDepositOrderCommandHandlerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingReviewedEvidenceCannotCreateFinancialRecords(bool historicalQuote)
    {
        var quote = new ReservationQuote();
        var quotes = new FakeReservationQuoteRepository();
        if (historicalQuote) quotes.Quotes.Add(quote);
        var work = new FakeUnitOfWork();
        var facts = new RecordingReservationNotifications();
        var handler = new EnsureReservationDepositOrderCommandHandler(new FakePaymentOrderRepository(), quotes, work, facts);
        var now = DateTimeOffset.UtcNow;
        var request = new EnsureReservationOrderV1(Guid.NewGuid(), quote.AccountId, Guid.NewGuid(), Guid.NewGuid(),
            quote.Id, Guid.NewGuid(), "51A12345", now.AddHours(1), now.AddHours(2), "PAYOS", 0, "VND", true, "covered", null);
        var error = await Assert.ThrowsAsync<PaymentDomainException>(() => handler.Handle(new(request), default));
        Assert.Equal("QUOTE_NOT_FOUND", error.Code);
        Assert.Empty(facts.Receipts);
        Assert.Empty(facts.Hints);
        Assert.Equal(0, work.SaveCount);
    }
}
