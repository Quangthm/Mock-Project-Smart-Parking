using System.Text.Json.Serialization;

namespace SmartParking.OperationService.Application.PassPlans.DTOs;

public sealed record SavePassPlanDraftResponseDto(
    [property: JsonPropertyName("planId")] Guid PlanId,
    [property: JsonPropertyName("siteId")] Guid SiteId,
    [property: JsonPropertyName("productType")] string ProductType,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("targetRevision")] int TargetRevision,
    [property: JsonPropertyName("updatedAt")] DateTimeOffset UpdatedAt);
