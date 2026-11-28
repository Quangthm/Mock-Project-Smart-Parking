using System.Text.Json.Serialization;

namespace SmartParking.OperationService.Application.PassPlans.DTOs;

public sealed record PassPlanSummaryDto(
    [property: JsonPropertyName("planId")] Guid? PlanId,
    [property: JsonPropertyName("productType")] string ProductType,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("isEnabled")] bool IsEnabled,
    [property: JsonPropertyName("hasDraft")] bool HasDraft,
    [property: JsonPropertyName("activeRevision")] int ActiveRevision,
    [property: JsonPropertyName("entitlementType")] string EntitlementType,
    [property: JsonPropertyName("zoneId")] Guid? ZoneId,
    [property: JsonPropertyName("vehicleTypes")] IReadOnlyList<string> VehicleTypes,
    [property: JsonPropertyName("ordinaryCoverageRules")] OrdinaryCoverageRulesDto? OrdinaryCoverageRules,
    [property: JsonPropertyName("financialReadiness")] PassFinancialReadinessDto FinancialReadiness,
    [property: JsonPropertyName("updatedAt")] DateTimeOffset? UpdatedAt);
