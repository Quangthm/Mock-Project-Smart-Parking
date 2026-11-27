namespace SmartParking.OperationService.Application.Reservations;

public interface IReservationProgressRelay
{
    Task CheckoutChangedAsync(Guid accountId, Guid reservationId, CancellationToken ct);
    Task PaymentSucceededAsync(Guid accountId, ReservationPaymentSucceededNotification payment, CancellationToken ct);
    Task DeliverAsync(Guid accountId, ReservationProgress progress, CancellationToken ct);
}
