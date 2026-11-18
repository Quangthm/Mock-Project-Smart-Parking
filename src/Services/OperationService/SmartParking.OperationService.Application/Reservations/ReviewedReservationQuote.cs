namespace SmartParking.OperationService.Application.Reservations;

public sealed record ReviewedReservationQuote(
    Guid QuoteId, string QuoteVersion, DateTimeOffset ValidUntil, string CanonicalPlate, Guid SiteId,
    Guid RequestedSlotId, DateTimeOffset Start, DateTimeOffset End, string PaymentMethod,
    string TariffVersion, bool IsCovered, string CoverageVersion, decimal BookedAmount, decimal CoveredAmount,
    decimal DepositAmount, string Currency, string Disclosure, ReviewedOperationalPolicy OperationalPolicy);
