namespace SmartParking.OperationService.Application.Reservations;

public sealed record VerifiedCollection(Guid CollectionId, Guid OrderId, Guid ReservationId, Guid AccountId,
    Guid TenantId, Guid SiteId, Guid QuoteId, string TermsVersion, decimal Amount, string Currency);
