using System.Text.Json;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Pricing;
namespace SmartParking.PaymentService.Application.Reservations;

public sealed record ReviewedQuoteProof(Guid QuoteId, string QuoteVersion, string Status, DateTimeOffset ValidUntil,
    ReviewedQuoteBinding Binding, string TariffVersion, decimal BookedAmount, decimal CoveredAmount,
    decimal DepositAmount, string Currency, string PrepaymentMode, string Disclosure, PricingCalculationResult Breakdown)
{
    public static ReviewedQuoteProof From(ReservationQuote quote)
    {
        var proof = JsonSerializer.Deserialize<ReviewedQuoteProof>(quote.ReviewedEvidence!)
            ?? throw new InvalidOperationException("Missing reviewed evidence.");
        return proof with { Status = quote.Status.ToString() };
    }
}
