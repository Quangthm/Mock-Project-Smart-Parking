namespace SmartParking.OperationService.Application.Reservations;

public sealed record ReservationPaymentSucceededNotification(Guid EventId, Guid ReservationId, Guid PaymentOrderId,
    Guid PaymentTransactionId, decimal Amount, string Currency, DateTimeOffset OccurredAt)
{
    public string Outcome => "SUCCEEDED";
}
