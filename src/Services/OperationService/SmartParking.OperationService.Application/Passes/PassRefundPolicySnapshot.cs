namespace SmartParking.OperationService.Application.Passes;

public sealed record PassRefundPolicySnapshot(
    decimal VoluntaryBeforeStartPercent,
    decimal VoluntaryInPeriodPercent,
    decimal FacilityClosurePercent,
    string RoundingMode);
