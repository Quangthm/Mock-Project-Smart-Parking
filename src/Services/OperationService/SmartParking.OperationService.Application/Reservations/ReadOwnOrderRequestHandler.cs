using MediatR;

namespace SmartParking.OperationService.Application.Reservations;

public sealed class ReadOwnOrderRequestHandler(ICurrentActor actor, IReservationRequestRepository requests)
    : IRequestHandler<ReadOwnOrderRequest, Guid?>
{
    public Task<Guid?> Handle(ReadOwnOrderRequest query, CancellationToken ct)
    {
        var (_, account) = AdmissionActor.RequireDriver(actor);
        return requests.FindOwnByOrderAsync(account, query.OrderId, ct);
    }
}
