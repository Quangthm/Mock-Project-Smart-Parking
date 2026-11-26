using System.Text.Json.Serialization;

namespace SmartParking.OperationService.Infrastructure.PassPlans.Models;

internal sealed record FacilityCapacityViewDto(
    [property: JsonPropertyName("unitId")] Guid? UnitId,
    [property: JsonPropertyName("vehicleType")] string VehicleType,
    [property: JsonPropertyName("total")] int Total);
