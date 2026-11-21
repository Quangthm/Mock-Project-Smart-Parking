namespace SmartParking.OperationService.Application.Reservations;

public interface IReservationProgressPublisher
{
    Task SendAsync(ReservationProgressChanged change, CancellationToken ct);
}
