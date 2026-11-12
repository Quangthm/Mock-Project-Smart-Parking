namespace SmartParking.PaymentService.Application.DTOs.Reservations;

public sealed record ReservationPricePreviewRequestDto(
    Guid SiteId,
    Guid RequestedSlotId,
    DateTimeOffset Start,
    DateTimeOffset End,
    Guid? VehicleId = null,
    string? VehicleType = "CAR",
    string? PaymentMethod = null,
    Guid? TenantId = null
);

public sealed record ReservationPricePreviewResponseDto(
    decimal BookedAmount,
    decimal? DepositAmount,
    string Currency,
    string PricingVersion,
    bool RequiresCompleteSelection
);

public sealed record CreateReservationQuoteRequestDto(
    Guid SiteId,
    Guid RequestedSlotId,
    DateTimeOffset Start,
    DateTimeOffset End,
    Guid VehicleId,
    string PaymentMethod,
    Guid? AccountId = null,
    Guid? TenantId = null,
    string? VehicleType = "CAR",
    string? PrepaymentMode = "FULL",
    string? CoverageVersion = "STANDARD_v1"
);

public sealed record BookingSelectionDto(
    Guid VehicleId,
    Guid SiteId,
    Guid RequestedSlotId,
    DateTimeOffset Start,
    DateTimeOffset End,
    string PaymentMethod
);

public sealed record CommercialTermsResponseDto(
    Guid QuoteId,
    string Version,
    DateTimeOffset ValidUntil,
    BookingSelectionDto Selection,
    Guid AccountId,
    decimal BookedAmount,
    decimal DepositAmount,
    string Currency,
    string PrepaymentMode,
    string CoverageVersion,
    string Disclosure
);

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
    DateTimeOffset ExpiresAt
);

public sealed record EnsureReservationDepositOrderResponseDto(
    string State,
    Guid? OrderId,
    EnsureReservationDepositOrderRequestDto AcceptedRequest
);

public sealed record VerifiedCollectionDto(
    Guid CollectionId,
    Guid OrderId,
    Guid ReservationId,
    Guid AccountId,
    Guid TenantId,
    Guid SiteId,
    Guid QuoteId,
    string TermsVersion,
    decimal Amount,
    string Currency
);

public sealed record CollectionLookupResponseDto(
    string State,
    IReadOnlyList<VerifiedCollectionDto> Collections
);

public sealed record ReconcileReservationRequestDto(
    Guid ReservationId,
    Guid CollectionId,
    decimal Amount,
    string Reason
);

public sealed record ReconcileReservationResponseDto(
    Guid RefundRequestId,
    Guid ReservationId,
    Guid CollectionId,
    string Status,
    string Reason,
    DateTimeOffset CreatedAt
);
