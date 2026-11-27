using MediatR;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record PricePreview(decimal BookedAmount, decimal? DepositAmount, string Currency,
    string PricingVersion, bool RequiresCompleteSelection);
