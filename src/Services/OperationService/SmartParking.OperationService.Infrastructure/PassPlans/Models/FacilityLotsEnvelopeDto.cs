using System.Text.Json.Serialization;

namespace SmartParking.OperationService.Infrastructure.PassPlans.Models;

internal sealed record FacilityLotsEnvelopeDto(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("data")] IReadOnlyList<FacilityLotSummaryDto>? Data);
