namespace SmartParking.PaymentService.Application.DTOs.Reservations;

public sealed record ReconcileReservationResponseDto(
    Guid RefundRequestId,
    Guid ReservationId,
    Guid CollectionId,
    string Status,
    string Reason,
    DateTimeOffset CreatedAt
);
