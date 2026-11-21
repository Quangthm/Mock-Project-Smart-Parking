using MediatR;
namespace SmartParking.PaymentService.Application.Reservations;

public sealed record IssueReviewedQuote(ReviewedQuoteBinding Binding) : IRequest<ReviewedQuoteProof>;
