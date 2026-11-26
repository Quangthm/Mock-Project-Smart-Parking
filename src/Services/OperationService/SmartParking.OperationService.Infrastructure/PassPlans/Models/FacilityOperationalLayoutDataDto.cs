using System.Text.Json.Serialization;

namespace SmartParking.OperationService.Infrastructure.PassPlans.Models;

internal sealed record FacilityOperationalLayoutDataDto(
    [property: JsonPropertyName("siteId")] Guid SiteId,
    [property: JsonPropertyName("metrics")] FacilityOperationalLayoutMetricsDto? Metrics);
