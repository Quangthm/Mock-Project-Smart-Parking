using System.Text.Json.Serialization;

namespace SmartParking.PaymentService.Infrastructure.Adapters.Outbound.Models;

internal sealed record FacilityOperationalLayoutEnvelopeHttpDto(
    [property: JsonPropertyName("success")] bool Success);
