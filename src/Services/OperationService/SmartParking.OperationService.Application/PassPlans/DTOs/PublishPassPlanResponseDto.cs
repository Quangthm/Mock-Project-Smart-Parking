using System.Text.Json.Serialization;

namespace SmartParking.OperationService.Application.PassPlans.DTOs;

public sealed record PublishPassPlanResponseDto(
    [property: JsonPropertyName("planId")] Guid PlanId,
    [property: JsonPropertyName("siteId")] Guid SiteId,
    [property: JsonPropertyName("productType")] string ProductType,
    [property: JsonPropertyName("publishedRevision")] int PublishedRevision,
    [property: JsonPropertyName("effectiveFrom")] DateTimeOffset EffectiveFrom,
    [property: JsonPropertyName("isEnabled")] bool IsEnabled,
    [property: JsonPropertyName("isReplayed")] bool IsReplayed);
