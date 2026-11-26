using MediatR;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.DTOs.PassPlans;

namespace SmartParking.PaymentService.Application.Usecase.PassPlans;

public sealed class GetInternalPassFinancialTermsQueryHandler(
    ISubscriptionFinancialTermRepository repository)
    : IRequestHandler<GetInternalPassFinancialTermsQuery, IReadOnlyList<PassFinancialTermLookupItemDto>>
{
    public async Task<IReadOnlyList<PassFinancialTermLookupItemDto>> Handle(
        GetInternalPassFinancialTermsQuery query,
        CancellationToken ct)
    {
        var terms = await repository.GetActiveBySiteAsync(query.SiteId, query.PlanId, ct);
        var items = new List<PassFinancialTermLookupItemDto>(terms.Count);

        foreach (var term in terms)
        {
            var activeRev = term.Revisions.FirstOrDefault(r => r.RevisionNumber == term.ActiveRevision);
            if (activeRev is null)
            {
                continue;
            }

            items.Add(new PassFinancialTermLookupItemDto(
                term.Id,
                term.SiteId,
                term.PlanId,
                activeRev.PlanRevision,
                activeRev.RevisionNumber,
                term.ProductType,
                term.Currency,
                term.IsActive,
                activeRev.VehiclePrices
                    .Select(vp => new PassVehiclePriceDto(vp.VehicleType, vp.FullPeriodPrice))
                    .ToArray(),
                new PassRefundPolicyDto(
                    activeRev.RefundPolicy.VoluntaryBeforeStartPercent,
                    activeRev.RefundPolicy.VoluntaryInPeriodPercent,
                    activeRev.RefundPolicy.FacilityClosurePercent,
                    activeRev.RefundPolicy.RoundingMode),
                activeRev.EffectiveFrom));
        }

        return items;
    }
}
