using System.Text.Json.Serialization;

namespace SmartParking.OperationService.Infrastructure.PassPlans.Models;

internal sealed record FacilityOperationalLayoutZoneDto(
    [property: JsonPropertyName("zoneId")] Guid ZoneId,
    [property: JsonPropertyName("zoneCode")] string? ZoneCode);
