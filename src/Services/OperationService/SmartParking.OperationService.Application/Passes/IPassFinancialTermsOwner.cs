namespace SmartParking.OperationService.Application.Passes;

public interface IPassFinancialTermsOwner
{
    Task<PassFinancialTermsSnapshot> QuoteSnapshotAsync(PassFinancialTermsQuoteRequest request, CancellationToken ct);
}
