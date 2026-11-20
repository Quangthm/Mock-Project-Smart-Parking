using MediatR;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Domain.Exceptions;
namespace SmartParking.PaymentService.Application.Reservations;

public sealed class ReadReviewedQuoteHandler(IReservationQuoteRepository quotes) : IRequestHandler<ReadReviewedQuote, ReviewedQuoteProof>
{
    public async Task<ReviewedQuoteProof> Handle(ReadReviewedQuote query, CancellationToken ct)
    {
        var quote = await quotes.FindReviewedAsync(query.AccountId, query.QuoteId, query.Version, ct)
            ?? throw new PaymentDomainException("QUOTE_NOT_FOUND", "Reviewed quote was not found.");
        return ReviewedQuoteProof.From(quote);
    }
}
