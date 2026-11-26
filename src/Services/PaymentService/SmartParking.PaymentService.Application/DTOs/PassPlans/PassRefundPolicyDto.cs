using System.Text.Json.Serialization;

namespace SmartParking.PaymentService.Application.DTOs.PassPlans;

public sealed record PassRefundPolicyDto(
    [property: JsonPropertyName("voluntaryBeforeStartPercent")] int VoluntaryBeforeStartPercent,
    [property: JsonPropertyName("voluntaryInPeriodPercent")] int VoluntaryInPeriodPercent,
    [property: JsonPropertyName("facilityClosurePercent")] int FacilityClosurePercent,
    [property: JsonPropertyName("roundingMode")] string RoundingMode);
