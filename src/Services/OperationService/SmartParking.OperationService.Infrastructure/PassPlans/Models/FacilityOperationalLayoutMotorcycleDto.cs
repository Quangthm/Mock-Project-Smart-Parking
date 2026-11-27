using System.Text.Json.Serialization;

namespace SmartParking.OperationService.Infrastructure.PassPlans.Models;

internal sealed record FacilityOperationalLayoutMotorcycleDto(
    [property: JsonPropertyName("maxCapacity")] int MaxCapacity);
