using System.Security.Cryptography;
using System.Text;
using SmartParking.OperationService.Application.PassPlans.DTOs;
using SmartParking.OperationService.Domain.PassPlans;

namespace SmartParking.OperationService.Application.PassPlans.Services;

internal static class PassPlanHelper
{
    public static PassProductType ParseProductType(string? raw)
    {
        if (!string.IsNullOrWhiteSpace(raw) &&
            Enum.TryParse<PassProductType>(raw.Trim(), ignoreCase: true, out var parsed) &&
            Enum.IsDefined(parsed))
        {
            return parsed;
        }

        throw new PassPlanException(
            400,
            "VALIDATION_ERROR",
            "productType must be MONTHLY or YEARLY.",
            "productType");
    }

    public static PassVehicleType ParseVehicleType(string? raw, string fieldName = "vehicleType")
    {
        if (!string.IsNullOrWhiteSpace(raw) &&
            Enum.TryParse<PassVehicleType>(raw.Trim(), ignoreCase: true, out var parsed) &&
            Enum.IsDefined(parsed))
        {
            return parsed;
        }

        throw new PassPlanException(
            400,
            "VALIDATION_ERROR",
            $"{fieldName} must be CAR, MOTORCYCLE, or OVERSIZED.",
            fieldName);
    }

    public static string ComputePublishHash(
        Guid siteId,
        PassProductType productType,
        PassPlanDraftSpec draft,
        string? publishNote)
    {
        var vehicles = string.Join(",", draft.VehicleTypes.Select(v => v.ToString()).OrderBy(v => v, StringComparer.Ordinal));
        var excluded = string.Join(",", draft.OrdinaryCoverageRules.ExcludedZoneIds.OrderBy(g => g).Select(g => g.ToString("N")));
        var raw = string.Join(
            "|",
            siteId.ToString("N"),
            productType.ToString(),
            draft.ZoneId?.ToString("N") ?? "SITE",
            vehicles,
            draft.OrdinaryCoverageRules.DayCovered ? "1" : "0",
            draft.OrdinaryCoverageRules.NightCovered ? "1" : "0",
            excluded,
            draft.IsAvailableOnly ? "1" : "0",
            publishNote?.Trim() ?? string.Empty);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static PassFinancialReadinessDto EvaluateFinancialReadiness(
        PassProductType productType,
        ParkingPassPlan? plan,
        IReadOnlyList<PassVehicleType> requiredVehicles,
        IReadOnlyList<PaymentPassFinancialTermDto> siteTerms)
    {
        if (productType == PassProductType.YEARLY)
        {
            return new PassFinancialReadinessDto(false, null, null, "YEARLY_POLICY_BLOCKED_Q05");
        }

        if (plan is null)
        {
            return new PassFinancialReadinessDto(false, null, null, "PLAN_NOT_CONFIGURED");
        }

        var term = siteTerms.FirstOrDefault(t => t.PlanId == plan.Id && t.IsActive);
        if (term is null)
        {
            return new PassFinancialReadinessDto(false, null, null, "MISSING_FINANCIAL_TERMS");
        }

        if (plan.ActiveRevision > 0 && term.PlanRevision != plan.ActiveRevision)
        {
            return new PassFinancialReadinessDto(
                false,
                term.FinancialTermId,
                term.FinancialTermRevision,
                "FINANCIAL_TERMS_PLAN_REVISION_MISMATCH");
        }

        if (!string.Equals(term.Currency, "VND", StringComparison.OrdinalIgnoreCase))
        {
            return new PassFinancialReadinessDto(
                false,
                term.FinancialTermId,
                term.FinancialTermRevision,
                "MISSING_FINANCIAL_TERMS");
        }

        var hasValidRefundPolicy = term.RefundPolicy is not null &&
            term.RefundPolicy.VoluntaryBeforeStartPercent == 100 &&
            term.RefundPolicy.VoluntaryInPeriodPercent == 50 &&
            term.RefundPolicy.FacilityClosurePercent == 100 &&
            string.Equals(term.RefundPolicy.RoundingMode, "HALF_UP_THOUSAND", StringComparison.OrdinalIgnoreCase);

        if (!hasValidRefundPolicy)
        {
            return new PassFinancialReadinessDto(
                false,
                term.FinancialTermId,
                term.FinancialTermRevision,
                "MISSING_FINANCIAL_TERMS");
        }

        foreach (var vehicleType in requiredVehicles)
        {
            var price = term.VehiclePrices.FirstOrDefault(vp =>
                string.Equals(vp.VehicleType, vehicleType.ToString(), StringComparison.OrdinalIgnoreCase));
            if (price is null || price.FullPeriodPrice <= 0m)
            {
                return new PassFinancialReadinessDto(
                    false,
                    term.FinancialTermId,
                    term.FinancialTermRevision,
                    "MISSING_FINANCIAL_TERMS");
            }
        }

        return new PassFinancialReadinessDto(
            true,
            term.FinancialTermId,
            term.FinancialTermRevision,
            null);
    }
}
