using System.Text.Json.Serialization;

namespace SmartParking.OperationService.Application.PassPlans.DTOs;

public sealed record FrozenPassPlanSnapshotDto(
    [property: JsonPropertyName("planId")] Guid PlanId,
    [property: JsonPropertyName("planRevision")] int PlanRevision,
    [property: JsonPropertyName("productType")] string ProductType,
    [property: JsonPropertyName("entitlementType")] string EntitlementType,
    [property: JsonPropertyName("siteId")] Guid SiteId,
    [property: JsonPropertyName("zoneId")] Guid? ZoneId,
    [property: JsonPropertyName("vehicleType")] string VehicleType,
    [property: JsonPropertyName("timeZoneId")] string TimeZoneId,
    [property: JsonPropertyName("ordinaryCoverageRules")] OrdinaryCoverageRulesDto OrdinaryCoverageRules,
    [property: JsonPropertyName("effectivePolicyParams")] OfferPolicyParamsDto EffectivePolicyParams);
