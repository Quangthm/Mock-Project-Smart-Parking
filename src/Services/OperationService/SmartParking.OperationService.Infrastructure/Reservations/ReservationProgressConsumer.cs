using MassTransit;
using SmartParking.OperationService.Application.Reservations;

namespace SmartParking.OperationService.Infrastructure.Reservations;

public sealed class ReservationProgressConsumer(IReservationRequestRepository requests, IReservationBookingRepository bookings,
    IReservationProgressRelay relay) : IConsumer<ReservationProgressChanged>
{
    public async Task Consume(ConsumeContext<ReservationProgressChanged> context)
    {
        var change = context.Message;
        var ct = context.CancellationToken;
        var request = await requests.FindOwnAsync(change.AccountId, change.RequestId, ct);
        if (request is null) return;
        var booking = request.ReservationId is Guid id ? await bookings.FindBookingAsync(id, ct) : null;
        await relay.DeliverAsync(request.AccountId,
            ReservationProgress.From(request, booking), ct);
    }
}
