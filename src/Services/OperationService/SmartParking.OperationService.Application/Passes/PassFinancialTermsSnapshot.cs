namespace SmartParking.OperationService.Application.Passes;

public sealed record PassFinancialTermsSnapshot(
    Guid FinancialTermId,
    int FinancialTermRevision,
    Guid PlanId,
    int PlanRevision,
    string ProductType,
    string VehicleType,
    decimal FullPeriodPrice,
    string Currency,
    PassRefundPolicySnapshot RefundPolicySnapshot,
    DateTimeOffset QuotedAt);
