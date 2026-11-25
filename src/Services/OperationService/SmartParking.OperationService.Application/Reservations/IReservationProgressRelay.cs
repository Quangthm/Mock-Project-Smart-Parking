namespace SmartParking.OperationService.Application.Reservations;

public interface IReservationProgressRelay
{
    Task CheckoutChangedAsync(Guid accountId, Guid reservationId, CancellationToken ct);
    Task DeliverAsync(Guid accountId, ReservationProgress progress, CancellationToken ct);
}
