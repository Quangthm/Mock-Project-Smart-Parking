using MediatR;
using System.Text.Json;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Domain.Pricing;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Exceptions;
namespace SmartParking.PaymentService.Application.Reservations;

public sealed class IssueReviewedQuoteHandler(IReviewedTariffRepository tariffs, IParkingPricingCalculator calculator,
    IReservationQuoteRepository quotes, IUnitOfWork work, TimeProvider clock) : IRequestHandler<IssueReviewedQuote, ReviewedQuoteProof>
{
    public async Task<ReviewedQuoteProof> Handle(IssueReviewedQuote command, CancellationToken ct)
    {
        var b = command.Binding;
        var (revision, policy) = await tariffs.FindAsync(b.TenantId, b.SiteId, ct)
            ?? throw new PaymentDomainException("QUOTE_PRICING_UNAVAILABLE", "No reviewed tariff is available.");
        var breakdown = calculator.Calculate(new(b.TenantId, b.SiteId, PricingVehicleType.CAR, b.Start, b.End, policy));
        var booked = breakdown.FinalAmountVnd;
        var covered = b.Coverage.IsCovered ? booked : 0;
        var now = clock.GetUtcNow();
        var until = new DateTimeOffset(now.UtcTicks - now.UtcTicks % 10, TimeSpan.Zero).AddMinutes(10);
        var proof = new ReviewedQuoteProof(Guid.NewGuid(), $"rq1-{Guid.NewGuid():N}", "ISSUED", until, b,
            revision.ToString("D"), booked, covered, booked - covered, breakdown.Currency, "FULL",
            "Non-refundable deposit applies if cancelled within 15 minutes of reservation start time.", breakdown);
        var quote = new ReservationQuote
        {
            Id = proof.QuoteId, TermsVersion = proof.QuoteVersion, AccountId = b.AccountId, TenantId = b.TenantId,
            SiteId = b.SiteId, RequestedSlotId = b.RequestedSlotId, StartTime = b.Start.ToUniversalTime(),
            EndTime = b.End.ToUniversalTime(), BookedAmount = booked, DepositAmount = proof.DepositAmount,
            Currency = proof.Currency, PrepaymentMode = PrepaymentMode.FULL, TariffId = policy.TariffId,
            TariffRevisionId = revision, CoverageVersion = b.Coverage.Version, ValidUntil = until,
            ReviewedEvidence = JsonSerializer.Serialize(proof), CreatedAt = now, UpdatedAt = now
        };
        await quotes.AddAsync(quote, ct);
        await work.SaveChangesAsync(ct);
        return proof;
    }
}
