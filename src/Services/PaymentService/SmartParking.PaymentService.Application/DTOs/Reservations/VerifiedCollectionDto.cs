namespace SmartParking.PaymentService.Application.DTOs.Reservations;

public sealed record VerifiedCollectionDto(
    Guid CollectionId,
    Guid OrderId,
    Guid ReservationId,
    Guid AccountId,
    Guid TenantId,
    Guid SiteId,
    Guid QuoteId,
    string? TermsVersion,
    decimal Amount,
    string Currency,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    SmartParking.PaymentService.Application.Reservations.VerifiedTransferEvidence? Evidence = null
);
