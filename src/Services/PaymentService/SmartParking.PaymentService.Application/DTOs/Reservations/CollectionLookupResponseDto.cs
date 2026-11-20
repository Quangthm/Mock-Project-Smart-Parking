namespace SmartParking.PaymentService.Application.DTOs.Reservations;

public sealed record CollectionLookupResponseDto(
    string State,
    IReadOnlyList<VerifiedCollectionDto> Collections
);
