using SmartParking.PaymentService.Application.Common.Interfaces;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Application.DTOs.Invoices;
using SmartParking.PaymentService.Domain.Entities;

namespace SmartParking.PaymentService.Tests.TestDoubles;

public class FakeReservationQuoteRepository : IReservationQuoteRepository
{
    public readonly List<ReservationQuote> Quotes = new();
    public Task<ReservationQuote?> FindReviewedAsync(Guid accountId, Guid id, CancellationToken ct) =>
        Task.FromResult(Quotes.SingleOrDefault(q => q.Id == id && q.AccountId == accountId && q.ReviewedEvidence != null));

    public Task<ReservationQuote?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Quotes.FirstOrDefault(x => x.Id == id));
    }

    public Task AddAsync(ReservationQuote quote, CancellationToken cancellationToken = default)
    {
        Quotes.Add(quote);
        return Task.CompletedTask;
    }

    public void Update(ReservationQuote quote)
    {
        var idx = Quotes.FindIndex(x => x.Id == quote.Id);
        if (idx >= 0) Quotes[idx] = quote;
        else Quotes.Add(quote);
    }
}
