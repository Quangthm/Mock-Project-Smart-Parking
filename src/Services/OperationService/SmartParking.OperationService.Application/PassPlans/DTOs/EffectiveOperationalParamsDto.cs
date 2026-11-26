using System.Text.Json.Serialization;

namespace SmartParking.OperationService.Application.PassPlans.DTOs;

public sealed record EffectiveOperationalParamsDto(
    [property: JsonPropertyName("monthlyMaxPrepaidTerms")] int MonthlyMaxPrepaidTerms,
    [property: JsonPropertyName("monthlyPaymentHoldMinutes")] int MonthlyPaymentHoldMinutes,
    [property: JsonPropertyName("monthlyRenewalWindowDays")] int MonthlyRenewalWindowDays);
