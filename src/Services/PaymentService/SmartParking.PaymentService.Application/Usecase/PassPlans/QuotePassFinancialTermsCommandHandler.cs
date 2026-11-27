using MediatR;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.DTOs.PassPlans;
using SmartParking.PaymentService.Domain.Exceptions;

namespace SmartParking.PaymentService.Application.Usecase.PassPlans;

public sealed class QuotePassFinancialTermsCommandHandler(
    ISubscriptionFinancialTermRepository repository,
    TimeProvider clock) : IRequestHandler<QuotePassFinancialTermsCommand, PassFinancialQuoteSnapshotDto>
{
    public async Task<PassFinancialQuoteSnapshotDto> Handle(
        QuotePassFinancialTermsCommand command,
        CancellationToken ct)
    {
        var req = command.Request;
        var productType = req.ProductType.Trim().ToUpperInvariant();
        var vehicleType = req.VehicleType.Trim().ToUpperInvariant();

        if (productType == "YEARLY")
        {
            throw new PaymentDomainException(
                "YEARLY_POLICY_BLOCKED_Q05",
                "Yearly parking products are currently blocked pending policy approval Q05.",
                "productType");
        }

        var term = await repository.GetBySiteAndPlanAsync(req.SiteId, req.PlanId, ct);
        if (term is null || !term.IsActive)
        {
            throw new PaymentDomainException(
                "FINANCIAL_TERMS_MISSING",
                $"No active financial terms exist for pass plan '{req.PlanId}'.");
        }

        var activeRev = term.Revisions.FirstOrDefault(r => r.RevisionNumber == term.ActiveRevision);
        if (activeRev is null)
        {
            throw new PaymentDomainException(
                "FINANCIAL_TERMS_MISSING",
                $"Active financial term revision for pass plan '{req.PlanId}' was not found.");
        }

        if (term.ActiveRevision != req.ExpectedFinancialTermRevision)
        {
            throw new PaymentDomainException(
                "FINANCIAL_TERMS_CHANGED",
                $"Financial terms updated from revision {req.ExpectedFinancialTermRevision} to {term.ActiveRevision}.");
        }

        if (activeRev.PlanRevision < req.PlanRevision)
        {
            throw new PaymentDomainException(
                "FINANCIAL_TERMS_MISSING",
                $"Financial terms have not yet been configured for plan revision {req.PlanRevision}.");
        }

        if (activeRev.PlanRevision != req.PlanRevision)
        {
            throw new PaymentDomainException(
                "FINANCIAL_TERMS_CHANGED",
                $"Financial terms target plan revision {activeRev.PlanRevision} rather than {req.PlanRevision}.");
        }

        var price = activeRev.VehiclePrices.FirstOrDefault(vp =>
            string.Equals(vp.VehicleType, vehicleType, StringComparison.OrdinalIgnoreCase));

        if (price is null || price.FullPeriodPrice <= 0m)
        {
            throw new PaymentDomainException(
                "FINANCIAL_TERMS_MISSING",
                $"No active price configured for vehicle type '{vehicleType}' on pass plan '{req.PlanId}'.");
        }

        return new PassFinancialQuoteSnapshotDto(
            term.Id,
            activeRev.RevisionNumber,
            term.PlanId,
            activeRev.PlanRevision,
            term.ProductType,
            vehicleType,
            price.FullPeriodPrice,
            term.Currency,
            new PassRefundPolicyDto(
                activeRev.RefundPolicy.VoluntaryBeforeStartPercent,
                activeRev.RefundPolicy.VoluntaryInPeriodPercent,
                activeRev.RefundPolicy.FacilityClosurePercent,
                activeRev.RefundPolicy.RoundingMode),
            clock.GetUtcNow());
    }
}
