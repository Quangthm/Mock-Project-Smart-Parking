namespace SmartParking.OperationService.Application.Passes;

public sealed record PreparePassPurchaseResult(
    PassPlanSnapshot PlanSnapshot,
    PassFinancialTermsSnapshot FinancialTermsSnapshot);
