using System.Text.Json.Serialization;

namespace SmartParking.OperationService.Application.PassPlans.DTOs;

public sealed record SetPassPlanEnablementResponseDto(
    [property: JsonPropertyName("planId")] Guid PlanId,
    [property: JsonPropertyName("productType")] string ProductType,
    [property: JsonPropertyName("activeRevision")] int ActiveRevision,
    [property: JsonPropertyName("isEnabled")] bool IsEnabled,
    [property: JsonPropertyName("updatedAt")] DateTimeOffset UpdatedAt);
