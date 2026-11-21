namespace SmartParking.OperationService.Application.Reservations;

public interface IReservationProgressRelay
{
    Task DeliverAsync(Guid accountId, ReservationProgress progress, CancellationToken ct);
}
