namespace SmartParking.OperationService.Application.Reservations;

public interface IQuoteAdmissionStarter
{
    Task StartAsync(Guid requestId, DateTimeOffset deadline, CancellationToken ct);
}
