using System.Text.Json.Serialization;

namespace SmartParking.OperationService.Infrastructure.PassPlans.Models;

internal sealed record PaymentFinancialTermRefundPolicyHttpDto(
    [property: JsonPropertyName("voluntaryBeforeStartPercent")] int VoluntaryBeforeStartPercent,
    [property: JsonPropertyName("voluntaryInPeriodPercent")] int VoluntaryInPeriodPercent,
    [property: JsonPropertyName("facilityClosurePercent")] int FacilityClosurePercent,
    [property: JsonPropertyName("roundingMode")] string RoundingMode);
