namespace SmartParking.PaymentService.Domain.Entities;

public sealed record SubscriptionRefundPolicy(
    int VoluntaryBeforeStartPercent,
    int VoluntaryInPeriodPercent,
    int FacilityClosurePercent,
    string RoundingMode)
{
    public static SubscriptionRefundPolicy M05Default => new(100, 50, 100, "HALF_UP_THOUSAND");

    public bool IsM05Compliant() =>
        VoluntaryBeforeStartPercent == 100 &&
        VoluntaryInPeriodPercent == 50 &&
        FacilityClosurePercent == 100 &&
        string.Equals(RoundingMode, "HALF_UP_THOUSAND", StringComparison.OrdinalIgnoreCase);
}
