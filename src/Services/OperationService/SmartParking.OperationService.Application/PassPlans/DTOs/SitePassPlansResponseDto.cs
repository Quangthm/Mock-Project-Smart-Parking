using System.Text.Json.Serialization;

namespace SmartParking.OperationService.Application.PassPlans.DTOs;

public sealed record SitePassPlansResponseDto(
    [property: JsonPropertyName("siteId")] Guid SiteId,
    [property: JsonPropertyName("timeZoneId")] string TimeZoneId,
    [property: JsonPropertyName("effectiveOperationalParams")] EffectiveOperationalParamsDto EffectiveOperationalParams,
    [property: JsonPropertyName("plans")] IReadOnlyList<PassPlanSummaryDto> Plans);
