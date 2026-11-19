using MediatR;
using Microsoft.Extensions.Options;
using SmartParking.OperationService.Application.PassPlans.Commands;
using SmartParking.OperationService.Application.PassPlans.DTOs;
using SmartParking.OperationService.Application.PassPlans.Options;
using SmartParking.OperationService.Application.PassPlans.Ports;
using SmartParking.OperationService.Application.PassPlans.Services;
using SmartParking.OperationService.Domain.PassPlans;

namespace SmartParking.OperationService.Application.PassPlans.Handlers;

public sealed class ValidateAndFreezePassPlanCommandHandler(
    IPassPlanRepository repository,
    IOptions<PassPlanDefaultsOptions> defaultsOptions)
    : IRequestHandler<ValidateAndFreezePassPlanCommand, FrozenPassPlanSnapshotDto>
{
    public async Task<FrozenPassPlanSnapshotDto> Handle(ValidateAndFreezePassPlanCommand command, CancellationToken ct)
    {
        var req = command.Request;
        var productType = PassPlanHelper.ParseProductType(req.ProductType);
        var vehicleType = PassPlanHelper.ParseVehicleType(req.VehicleType);

        if (productType == PassProductType.YEARLY)
        {
            throw new PassPlanException(
                422,
                "YEARLY_POLICY_BLOCKED_Q05",
                "Yearly parking products are currently blocked pending policy approval Q05.");
        }

        var plan = await repository.GetPlanByIdAsync(req.SiteId, req.PlanId, ct);
        if (plan is null || plan.ProductType != productType)
        {
            throw new PassPlanException(
                404,
                "PASS_PLAN_NOT_FOUND",
                $"Pass plan '{req.PlanId}' was not found for site '{req.SiteId}'.");
        }

        if (plan.ActiveRevision <= 0)
        {
            throw new PassPlanException(
                422,
                "PLAN_NOT_PURCHASABLE",
                "Pass plan has no published revision.");
        }

        if (plan.ActiveRevision != req.ExpectedPlanRevision)
        {
            throw new PassPlanException(
                409,
                "PLAN_VERSION_CHANGED",
                $"Pass plan '{plan.Id}' is now at revision {plan.ActiveRevision} (expected {req.ExpectedPlanRevision}).");
        }

        if (!plan.IsEnabled)
        {
            throw new PassPlanException(
                422,
                "PLAN_NOT_PURCHASABLE",
                "Pass plan is currently disabled for new purchases.");
        }

        var revision = await repository.GetRevisionAsync(plan.Id, plan.ActiveRevision, ct);
        if (revision is null)
        {
            throw new PassPlanException(
                404,
                "PASS_PLAN_NOT_FOUND",
                $"Published revision {plan.ActiveRevision} for pass plan '{plan.Id}' was not found.");
        }

        if (!revision.VehicleTypes.Contains(vehicleType))
        {
            throw new PassPlanException(
                422,
                "PLAN_NOT_PURCHASABLE",
                $"Vehicle type '{vehicleType}' is not covered by pass plan '{plan.Id}'.");
        }

        var defaults = defaultsOptions.Value;
        var timeZoneId = string.IsNullOrWhiteSpace(revision.ContractTimezone)
            ? await repository.GetEffectiveTimeZoneAsync(req.SiteId, ct)
            : revision.ContractTimezone;

        return new FrozenPassPlanSnapshotDto(
            PlanId: plan.Id,
            PlanRevision: revision.RevisionNumber,
            ProductType: plan.ProductType.ToString(),
            EntitlementType: plan.EntitlementType,
            SiteId: plan.SiteId,
            ZoneId: revision.ZoneId,
            VehicleType: vehicleType.ToString(),
            TimeZoneId: timeZoneId,
            OrdinaryCoverageRules: new OrdinaryCoverageRulesDto(
                revision.OrdinaryCoverageRules.DayCovered,
                revision.OrdinaryCoverageRules.NightCovered,
                revision.OrdinaryCoverageRules.ExcludedZoneIds),
            EffectivePolicyParams: new OfferPolicyParamsDto(
                defaults.MonthlyMaxPrepaidTerms,
                defaults.MonthlyPaymentHoldMinutes,
                defaults.MonthlyRenewalWindowDays));
    }
}
