using MediatR;
namespace SmartParking.PaymentService.Application.Reservations;

public sealed record ReadReviewedQuote(Guid AccountId, Guid QuoteId) : IRequest<ReviewedQuoteProof>;
