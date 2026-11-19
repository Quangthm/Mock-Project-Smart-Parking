using Microsoft.AspNetCore.SignalR;
using SmartParking.OperationService.API.Hubs;
using SmartParking.OperationService.Application.Reservations;

namespace SmartParking.OperationService.API.Realtime;

public sealed class ReservationProgressRelay(IHubContext<ReservationProgressHub> hub) : IReservationProgressRelay
{
    public Task DeliverAsync(Guid accountId, ReservationProgress progress, CancellationToken ct) =>
        hub.Clients.Group(ReservationProgressHub.Group(accountId, progress.RequestId))
            .SendAsync("ReservationProgress", progress, ct);
}
