namespace SmartParking.Contracts.ReservationPayments;

public static class ReservationPaymentEndpoints
{
    public const string EnsureOrder = "payment-reservation-order-v1";
    public const string ZeroReceipt = "operations-reservation-zero-receipt-v1";
    public const string Disposition = "payment-reservation-disposition-v1";
    public const string CheckoutChanged = "operations-reservation-checkout-hint-v1";
}
