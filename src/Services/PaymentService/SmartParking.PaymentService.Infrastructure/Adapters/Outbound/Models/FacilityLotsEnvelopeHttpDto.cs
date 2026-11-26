using System.Text.Json.Serialization;

namespace SmartParking.PaymentService.Infrastructure.Adapters.Outbound.Models;

internal sealed record FacilityLotsEnvelopeHttpDto(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("data")] IReadOnlyList<FacilityLotItemHttpDto>? Data);
