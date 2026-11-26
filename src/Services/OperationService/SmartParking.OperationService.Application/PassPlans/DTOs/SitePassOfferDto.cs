using System.Text.Json.Serialization;

namespace SmartParking.OperationService.Application.PassPlans.DTOs;

public sealed record SitePassOfferDto(
    [property: JsonPropertyName("planId")] Guid PlanId,
    [property: JsonPropertyName("planRevision")] int PlanRevision,
    [property: JsonPropertyName("financialTermId")] Guid FinancialTermId,
    [property: JsonPropertyName("financialTermRevision")] int FinancialTermRevision,
    [property: JsonPropertyName("productType")] string ProductType,
    [property: JsonPropertyName("entitlementType")] string EntitlementType,
    [property: JsonPropertyName("guaranteedCapacity")] bool GuaranteedCapacity,
    [property: JsonPropertyName("zoneId")] Guid? ZoneId,
    [property: JsonPropertyName("vehicleType")] string VehicleType,
    [property: JsonPropertyName("fullPeriodPrice")] decimal FullPeriodPrice,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("ordinaryCoverageRules")] OrdinaryCoverageRulesDto OrdinaryCoverageRules,
    [property: JsonPropertyName("effectivePolicyParams")] OfferPolicyParamsDto EffectivePolicyParams,
    [property: JsonPropertyName("anchorRule")] string AnchorRule,
    [property: JsonPropertyName("refundPolicySummary")] PaymentPassRefundPolicyDto RefundPolicySummary);
