namespace SmartParking.PaymentService.Application.DTOs.Reservations;

public sealed record EnsureReservationDepositOrderRequestDto(
    Guid OperationId,
    Guid ReservationId,
    Guid AccountId,
    Guid TenantId,
    Guid SiteId,
    Guid QuoteId,
    string TermsVersion,
    decimal Amount,
    string Currency,
    string PaymentMethod,
    DateTimeOffset ExpiresAt,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    SmartParking.PaymentService.Application.Reservations.ZeroQuoteSelection? ZeroSelection = null
);
