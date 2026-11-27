using SmartParking.Contracts.ReservationPayments;
namespace SmartParking.PaymentService.Application.Reservations;

public interface IReservationFinancialNotifications
{
    Task SendAsync(ReservationZeroReceiptIssuedV1 receipt, CancellationToken ct);
    Task SendAsync(ReservationCheckoutChangedV1 hint, CancellationToken ct);
}
