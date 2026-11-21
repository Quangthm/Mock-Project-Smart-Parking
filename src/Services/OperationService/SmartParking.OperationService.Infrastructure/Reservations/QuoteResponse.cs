namespace SmartParking.OperationService.Infrastructure.Reservations;

internal sealed record QuoteResponse(Guid QuoteId, string Version, DateTimeOffset ValidUntil,
    QuoteSelection Selection, Guid AccountId, decimal BookedAmount, decimal DepositAmount, string Currency,
    string PrepaymentMode, string CoverageVersion, string Disclosure);
