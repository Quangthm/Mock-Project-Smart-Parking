using System.Text.Json.Serialization;

namespace SmartParking.OperationService.Infrastructure.PassPlans.Models;

internal sealed record FacilityOperationalLayoutMetricsDto(
    [property: JsonPropertyName("motorcycleCapacity")] FacilityOperationalLayoutMotorcycleDto? MotorcycleCapacity,
    [property: JsonPropertyName("zoneCapacities")] IReadOnlyList<FacilityOperationalLayoutZoneDto>? ZoneCapacities);
