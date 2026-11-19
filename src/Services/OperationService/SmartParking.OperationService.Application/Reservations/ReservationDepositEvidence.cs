namespace SmartParking.OperationService.Application.Reservations;

public sealed record ReservationDepositEvidence(Guid EventId, Guid CollectionId, Guid PaymentOrderId,
    Guid PaymentTransactionId, Guid OperationId, Guid ReservationId, Guid AccountId, Guid TenantId, Guid SiteId,
    Guid QuoteId, string QuoteVersion, Guid? TermsId, string? TermsVersion, decimal Amount, string Currency,
    string Provider, string ProviderReference, DateTimeOffset ProviderVerifiedAt, DateTimeOffset OccurredAt, string? PaymentMethod = null)
{
    public VerifiedCollection Collection() => new(CollectionId, PaymentOrderId, ReservationId, AccountId,
        TenantId, SiteId, QuoteId, QuoteVersion, Amount, Currency);
}
