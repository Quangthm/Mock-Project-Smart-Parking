namespace SmartParking.OperationService.Infrastructure.Reservations;

internal sealed record CollectionItem(Guid CollectionId, Guid OrderId, Guid ReservationId, Guid AccountId,
    Guid TenantId, Guid SiteId, Guid QuoteId, string? TermsVersion, decimal Amount, string Currency, VerifiedTransferEvidence? Evidence);
