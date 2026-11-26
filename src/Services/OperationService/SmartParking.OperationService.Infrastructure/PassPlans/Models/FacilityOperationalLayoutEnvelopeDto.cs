using System.Text.Json.Serialization;

namespace SmartParking.OperationService.Infrastructure.PassPlans.Models;

internal sealed record FacilityOperationalLayoutEnvelopeDto(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("data")] FacilityOperationalLayoutDataDto? Data);
