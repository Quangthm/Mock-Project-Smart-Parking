using System.Text.Json.Serialization;

namespace SmartParking.OperationService.Infrastructure.PassPlans.Models;

internal sealed record FacilitySlotItemDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("unitId")] Guid UnitId,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("vehicleType")] string VehicleType,
    [property: JsonPropertyName("vehicleTypes")] IReadOnlyList<string>? VehicleTypes);
