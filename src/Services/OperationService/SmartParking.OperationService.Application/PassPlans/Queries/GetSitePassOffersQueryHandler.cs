using MediatR;
using Microsoft.Extensions.Options;
using SmartParking.OperationService.Application.PassPlans.DTOs;
using SmartParking.OperationService.Application.PassPlans.Options;
using SmartParking.OperationService.Application.PassPlans.Ports;
using SmartParking.OperationService.Application.PassPlans.Services;
using SmartParking.OperationService.Domain.PassPlans;

namespace SmartParking.OperationService.Application.PassPlans.Queries;

public sealed class GetSitePassOffersQueryHandler(
    IPassPlanFacilityScopePort scopePort,
    IPassPlanRepository repository,
    IPaymentPassTermsPort paymentTermsPort,
    IOptions<PassPlanDefaultsOptions> defaultsOptions)
    : IRequestHandler<GetSitePassOffersQuery, SitePassOffersResponseDto>
{
    private const string AnchorRuleCalendarMonth = "CALENDAR_MONTH_PRESERVE_ANCHOR_CLAMP_END";

    public async Task<SitePassOffersResponseDto> Handle(GetSitePassOffersQuery query, CancellationToken ct)
    {
        PassVehicleType? filterVehicle = string.IsNullOrWhiteSpace(query.VehicleType)
            ? null
            : PassPlanHelper.ParseVehicleType(query.VehicleType);

        var siteExists = await scopePort.SiteExistsAsync(query.SiteId, ct);
        if (!siteExists)
        {
            throw new PassPlanException(
                404,
                "SITE_NOT_FOUND",
                $"Parking site '{query.SiteId}' was not found.");
        }

        var plans = await repository.GetPlansBySiteAsync(query.SiteId, ct);
        var timeZoneId = await repository.GetEffectiveTimeZoneAsync(query.SiteId, ct);
        var defaults = defaultsOptions.Value;

        var monthlyPlan = plans.FirstOrDefault(p =>
            p.ProductType == PassProductType.MONTHLY &&
            p.IsEnabled &&
            p.ActiveRevision > 0);

        if (monthlyPlan is null)
        {
            return new SitePassOffersResponseDto(
                query.SiteId,
                timeZoneId,
                YearlyBlockedByQ05: true,
                Offers: []);
        }

        var activeRevision = await repository.GetRevisionAsync(monthlyPlan.Id, monthlyPlan.ActiveRevision, ct);
        if (activeRevision is null)
        {
            return new SitePassOffersResponseDto(
                query.SiteId,
                timeZoneId,
                YearlyBlockedByQ05: true,
                Offers: []);
        }

        var terms = await paymentTermsPort.GetActiveFinancialTermsAsync(query.SiteId, monthlyPlan.Id, ct);
        var readiness = PassPlanHelper.EvaluateFinancialReadiness(
            PassProductType.MONTHLY,
            monthlyPlan,
            activeRevision.VehicleTypes,
            terms);

        if (!readiness.HasCompleteFinancialTerms || readiness.FinancialTermId is null)
        {
            return new SitePassOffersResponseDto(
                query.SiteId,
                timeZoneId,
                YearlyBlockedByQ05: true,
                Offers: []);
        }

        var term = terms.FirstOrDefault(t =>
            t.FinancialTermId == readiness.FinancialTermId &&
            t.IsActive &&
            t.PlanRevision == monthlyPlan.ActiveRevision);

        if (term is null)
        {
            return new SitePassOffersResponseDto(
                query.SiteId,
                timeZoneId,
                YearlyBlockedByQ05: true,
                Offers: []);
        }

        var policyParams = new OfferPolicyParamsDto(
            defaults.MonthlyMaxPrepaidTerms,
            defaults.MonthlyPaymentHoldMinutes,
            defaults.MonthlyRenewalWindowDays);

        var coverageDto = new OrdinaryCoverageRulesDto(
            activeRevision.OrdinaryCoverageRules.DayCovered,
            activeRevision.OrdinaryCoverageRules.NightCovered,
            activeRevision.OrdinaryCoverageRules.ExcludedZoneIds);

        var offers = new List<SitePassOfferDto>();
        foreach (var vehicleType in activeRevision.VehicleTypes)
        {
            if (filterVehicle.HasValue && filterVehicle.Value != vehicleType)
            {
                continue;
            }

            var vehiclePrice = term.VehiclePrices.FirstOrDefault(vp =>
                string.Equals(vp.VehicleType, vehicleType.ToString(), StringComparison.OrdinalIgnoreCase));
            if (vehiclePrice is null || vehiclePrice.FullPeriodPrice <= 0m)
            {
                continue;
            }

            offers.Add(new SitePassOfferDto(
                PlanId: monthlyPlan.Id,
                PlanRevision: activeRevision.RevisionNumber,
                FinancialTermId: term.FinancialTermId,
                FinancialTermRevision: term.FinancialTermRevision,
                ProductType: monthlyPlan.ProductType.ToString(),
                EntitlementType: monthlyPlan.EntitlementType,
                GuaranteedCapacity: false,
                ZoneId: activeRevision.ZoneId,
                VehicleType: vehicleType.ToString(),
                FullPeriodPrice: vehiclePrice.FullPeriodPrice,
                Currency: term.Currency.Trim().ToUpperInvariant(),
                OrdinaryCoverageRules: coverageDto,
                EffectivePolicyParams: policyParams,
                AnchorRule: AnchorRuleCalendarMonth,
                RefundPolicySummary: term.RefundPolicy));
        }

        return new SitePassOffersResponseDto(
            query.SiteId,
            timeZoneId,
            YearlyBlockedByQ05: true,
            Offers: offers);
    }
}
