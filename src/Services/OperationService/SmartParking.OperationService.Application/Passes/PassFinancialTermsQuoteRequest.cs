namespace SmartParking.OperationService.Application.Passes;

public sealed record PassFinancialTermsQuoteRequest(
    Guid SiteId,
    Guid PlanId,
    int PlanRevision,
    int ExpectedFinancialTermRevision,
    string ProductType,
    string VehicleType);
