using SmartParking.Contracts.ReservationPayments;
using MassTransit;
using SmartParking.PaymentService.Application.Reservations;
namespace SmartParking.PaymentService.Infrastructure.Messaging;

public sealed class ReservationFinancialNotifications(ISendEndpointProvider endpoints) : IReservationFinancialNotifications
{
    public Task SendAsync(ReservationZeroReceiptIssuedV1 receipt, CancellationToken ct) =>
        SendAsync(ReservationPaymentEndpoints.ZeroReceipt, receipt, ct);
    public Task SendAsync(ReservationCheckoutChangedV1 hint, CancellationToken ct) =>
        SendAsync(ReservationPaymentEndpoints.CheckoutChanged, hint, ct);

    private async Task SendAsync<T>(string queue, T message, CancellationToken ct) where T : class
    {
        var endpoint = await endpoints.GetSendEndpoint(new Uri($"queue:{queue}"));
        await endpoint.Send(message, ct);
    }
}
