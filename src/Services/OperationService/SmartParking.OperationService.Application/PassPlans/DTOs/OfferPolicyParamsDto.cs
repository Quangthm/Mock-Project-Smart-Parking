using System.Text.Json.Serialization;

namespace SmartParking.OperationService.Application.PassPlans.DTOs;

public sealed record OfferPolicyParamsDto(
    [property: JsonPropertyName("maxPrepaidTerms")] int MaxPrepaidTerms,
    [property: JsonPropertyName("paymentHoldMinutes")] int PaymentHoldMinutes,
    [property: JsonPropertyName("renewalWindowDays")] int RenewalWindowDays);
