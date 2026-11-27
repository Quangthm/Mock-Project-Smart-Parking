namespace SmartParking.OperationService.Infrastructure.Reservations;

internal sealed record PreviewResponse(decimal BookedAmount, decimal? DepositAmount, string Currency,
    string PricingVersion, bool RequiresCompleteSelection);
