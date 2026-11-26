using System.Text.Json.Serialization;

namespace SmartParking.OperationService.Infrastructure.PassPlans.Models;

internal sealed record FacilityStructureEnvelopeDto(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("data")] FacilityStructureDataDto? Data);
