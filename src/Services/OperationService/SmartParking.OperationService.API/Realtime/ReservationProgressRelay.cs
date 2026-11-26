using Microsoft.AspNetCore.SignalR;
using SmartParking.OperationService.API.Hubs;
using SmartParking.OperationService.Application.Reservations;

namespace SmartParking.OperationService.API.Realtime;

public sealed class ReservationProgressRelay(IHubContext<ReservationProgressHub> hub,
    ILogger<ReservationProgressRelay> logger) : IReservationProgressRelay
{
    public Task CheckoutChangedAsync(Guid accountId, Guid reservationId, CancellationToken ct) =>
        hub.Clients.Group(ReservationProgressHub.Group(accountId, reservationId))
            .SendAsync("ReservationCheckoutChanged", reservationId, ct);

    public Task DeliverAsync(Guid accountId, ReservationProgress progress, CancellationToken ct) =>
        hub.Clients.Group(ReservationProgressHub.Group(accountId, progress.RequestId))
            .SendAsync("ReservationProgress", progress, ct);

    public async Task PaymentSucceededAsync(Guid accountId, ReservationPaymentSucceededNotification payment, CancellationToken ct)
    {
        logger.LogInformation("ReservationPaymentSucceeded SignalR send attempted. EventId {EventId}, ReservationId {ReservationId}, PaymentOrderId {PaymentOrderId}",
            payment.EventId, payment.ReservationId, payment.PaymentOrderId);
        await hub.Clients.Group(ReservationProgressHub.Group(accountId, payment.ReservationId))
            .SendAsync("ReservationPaymentSucceeded", payment, ct);
        logger.LogInformation("ReservationPaymentSucceeded SignalR send completed. EventId {EventId}, ReservationId {ReservationId}",
            payment.EventId, payment.ReservationId);
    }
}
