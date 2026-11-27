using MassTransit;
using SmartParking.OperationService.Application.Reservations;

namespace SmartParking.OperationService.Infrastructure.Reservations;

public sealed class ReservationProgressPublisher(ISendEndpointProvider endpoints) : IReservationProgressPublisher
{
    public const string Queue = "operations-reservation-progress-v1";
    public async Task SendAsync(ReservationProgressChanged change, CancellationToken ct)
    {
        var endpoint = await endpoints.GetSendEndpoint(new Uri($"queue:{Queue}"));
        await endpoint.Send(change, send => send.MessageId = change.EventId, ct);
    }
}
