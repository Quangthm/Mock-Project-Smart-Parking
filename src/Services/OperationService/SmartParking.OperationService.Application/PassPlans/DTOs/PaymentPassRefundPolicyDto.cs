using System.Text.Json.Serialization;

namespace SmartParking.OperationService.Application.PassPlans.DTOs;

public sealed record PaymentPassRefundPolicyDto(
    [property: JsonPropertyName("voluntaryBeforeStartPercent")] int VoluntaryBeforeStartPercent,
    [property: JsonPropertyName("voluntaryInPeriodPercent")] int VoluntaryInPeriodPercent,
    [property: JsonPropertyName("facilityClosurePercent")] int FacilityClosurePercent,
    [property: JsonPropertyName("roundingMode")] string RoundingMode);
