namespace SmartParking.OperationService.Application.PassPlans.Options;

public sealed class PassPlanDefaultsOptions
{
    public const string SectionName = "PassPlanDefaults";

    public int MonthlyMaxPrepaidTerms { get; set; } = 3;
    public int MonthlyPaymentHoldMinutes { get; set; } = 15;
    public int MonthlyRenewalWindowDays { get; set; } = 7;
}
