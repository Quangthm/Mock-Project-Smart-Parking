using System.Text.Json;
using System.Text.Json.Serialization;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Pricing;
namespace SmartParking.PaymentService.Application.Reservations;

public sealed record ReviewedQuoteProof(Guid QuoteId, string Status, DateTimeOffset ValidUntil,
    ReviewedQuoteBinding Binding, string TariffVersion, [property: JsonRequired] decimal BookedAmount, [property: JsonRequired] decimal CoveredAmount,
    [property: JsonRequired] decimal DepositAmount, string Currency, string PrepaymentMode, string Disclosure, PricingCalculationResult Breakdown)
{
    public static ReviewedQuoteProof From(ReservationQuote quote)
    {
        var proof = JsonSerializer.Deserialize<ReviewedQuoteProof>(quote.ReviewedEvidence!)
            ?? throw new InvalidOperationException("Missing reviewed evidence.");
        var binding = proof.Binding;
        if (binding?.Coverage is null || proof.Breakdown is null || proof.QuoteId != quote.Id ||
            binding.AccountId != quote.AccountId || binding.TenantId != quote.TenantId || binding.SiteId != quote.SiteId ||
            binding.RequestedSlotId != quote.RequestedSlotId || binding.Start != quote.StartTime || binding.End != quote.EndTime ||
            binding.Coverage.Version != quote.CoverageVersion || proof.ValidUntil != quote.ValidUntil ||
            proof.BookedAmount != quote.BookedAmount || proof.DepositAmount != quote.DepositAmount ||
            proof.BookedAmount != proof.Breakdown.FinalAmountVnd || proof.BookedAmount < 0 ||
            proof.CoveredAmount != (binding.Coverage.IsCovered ? proof.BookedAmount : 0m) ||
            proof.DepositAmount != proof.BookedAmount - proof.CoveredAmount || proof.Currency != quote.Currency ||
            proof.Currency != "VND" || proof.PrepaymentMode != "FULL" ||
            !Guid.TryParse(proof.TariffVersion, out var revision) || revision != quote.TariffRevisionId)
            throw new InvalidOperationException("Stored reviewed quote evidence is inconsistent.");
        return proof with { Status = quote.Status.ToString() };
    }
}
