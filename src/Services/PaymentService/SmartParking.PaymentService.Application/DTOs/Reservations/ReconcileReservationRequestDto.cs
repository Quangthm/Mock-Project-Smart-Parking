namespace SmartParking.PaymentService.Application.DTOs.Reservations;

public sealed record ReconcileReservationRequestDto(
    Guid ReservationId,
    Guid CollectionId,
    decimal Amount,
    string Reason
);
