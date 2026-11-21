namespace SmartParking.OperationService.Application.Reservations;

public interface IQuoteAdmissionStarter
{
    Task StartAsync(Guid requestId, CancellationToken ct);
}
