using System.Text.Json.Serialization;

namespace SmartParking.OperationService.Infrastructure.PassPlans.Models;

internal sealed record PaymentEnvelopeDto<T>(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("data")] T? Data);
