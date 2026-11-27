using SmartParking.Contracts.ReservationPayments;
using SmartParking.PaymentService.Application.Reservations;

namespace SmartParking.PaymentService.Tests.TestDoubles;

internal sealed class RecordingReservationNotifications : IReservationFinancialNotifications
{
    public List<ReservationZeroReceiptIssuedV1> Receipts { get; } = [];
    public List<ReservationCheckoutChangedV1> Hints { get; } = [];
    public Task SendAsync(ReservationZeroReceiptIssuedV1 receipt, CancellationToken ct)
    {
        Receipts.Add(receipt);
        return Task.CompletedTask;
    }
    public Task SendAsync(ReservationCheckoutChangedV1 hint, CancellationToken ct)
    {
        Hints.Add(hint);
        return Task.CompletedTask;
    }
}
