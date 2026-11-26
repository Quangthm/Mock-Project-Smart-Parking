using MediatR;
using Microsoft.Extensions.Options;
using SmartParking.OperationService.Application.PassPlans.DTOs;
using SmartParking.OperationService.Application.PassPlans.Options;
using SmartParking.OperationService.Application.PassPlans.Ports;
using SmartParking.OperationService.Application.PassPlans.Services;
using SmartParking.OperationService.Domain.PassPlans;

namespace SmartParking.OperationService.Application.PassPlans.Queries;

public sealed class GetSitePassPlansQueryHandler(
    IPassPlanFacilityScopePort scopePort,
    IPassPlanRepository repository,
    IPaymentPassTermsPort paymentTermsPort,
    IOptions<PassPlanDefaultsOptions> defaultsOptions)
    : IRequestHandler<GetSitePassPlansQuery, SitePassPlansResponseDto>
{
    public async Task<SitePassPlansResponseDto> Handle(GetSitePassPlansQuery query, CancellationToken ct)
    {
        await scopePort.ValidateOwnerSiteAsync(query.SiteId, ct);

        var plans = await repository.GetPlansBySiteAsync(query.SiteId, ct);
        var timeZoneId = await repository.GetEffectiveTimeZoneAsync(query.SiteId, ct);
        var defaults = defaultsOptions.Value;

        IReadOnlyList<PaymentPassFinancialTermDto> siteTerms = plans.Any(p => p.ProductType == PassProductType.MONTHLY)
            ? await paymentTermsPort.GetActiveFinancialTermsAsync(query.SiteId, null, ct)
            : [];

        var summaries = new List<PassPlanSummaryDto>(2);
        foreach (var productType in new[] { PassProductType.MONTHLY, PassProductType.YEARLY })
        {
            var plan = plans.FirstOrDefault(p => p.ProductType == productType);
            if (plan is null)
            {
                summaries.Add(new PassPlanSummaryDto(
                    PlanId: null,
                    ProductType: productType.ToString(),
                    Status: "UNCONFIGURED",
                    IsEnabled: false,
                    HasDraft: false,
                    ActiveRevision: 0,
                    EntitlementType: ParkingPassPlan.FixedEntitlementType,
                    ZoneId: null,
                    VehicleTypes: [],
                    OrdinaryCoverageRules: null,
                    FinancialReadiness: PassPlanHelper.EvaluateFinancialReadiness(productType, null, [], siteTerms),
                    UpdatedAt: null));
                continue;
            }

            Guid? zoneId;
            IReadOnlyList<PassVehicleType> vehicleTypes;
            OrdinaryCoverageRules coverageRules;

            if (plan.ActiveRevision > 0)
            {
                var activeRevision = await repository.GetRevisionAsync(plan.Id, plan.ActiveRevision, ct);
                zoneId = activeRevision?.ZoneId ?? plan.DraftSpec?.ZoneId;
                vehicleTypes = activeRevision?.VehicleTypes ?? plan.DraftSpec?.VehicleTypes ?? [];
                coverageRules = activeRevision?.OrdinaryCoverageRules
                    ?? plan.DraftSpec?.OrdinaryCoverageRules
                    ?? new OrdinaryCoverageRules(true, true, []);
            }
            else
            {
                zoneId = plan.DraftSpec?.ZoneId;
                vehicleTypes = plan.DraftSpec?.VehicleTypes ?? [];
                coverageRules = plan.DraftSpec?.OrdinaryCoverageRules
                    ?? new OrdinaryCoverageRules(true, true, []);
            }

            var readiness = PassPlanHelper.EvaluateFinancialReadiness(productType, plan, vehicleTypes, siteTerms);

            summaries.Add(new PassPlanSummaryDto(
                PlanId: plan.Id,
                ProductType: productType.ToString(),
                Status: plan.Status.ToString(),
                IsEnabled: plan.IsEnabled,
                HasDraft: plan.DraftSpec is not null,
                ActiveRevision: plan.ActiveRevision,
                EntitlementType: plan.EntitlementType,
                ZoneId: zoneId,
                VehicleTypes: vehicleTypes.Select(v => v.ToString()).ToArray(),
                OrdinaryCoverageRules: new OrdinaryCoverageRulesDto(
                    coverageRules.DayCovered,
                    coverageRules.NightCovered,
                    coverageRules.ExcludedZoneIds),
                FinancialReadiness: readiness,
                UpdatedAt: plan.UpdatedAt));
        }

        return new SitePassPlansResponseDto(
            query.SiteId,
            timeZoneId,
            new EffectiveOperationalParamsDto(
                defaults.MonthlyMaxPrepaidTerms,
                defaults.MonthlyPaymentHoldMinutes,
                defaults.MonthlyRenewalWindowDays),
            summaries);
    }
}
