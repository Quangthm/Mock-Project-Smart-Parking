namespace SmartParking.PaymentService.Domain.Enums;

public enum PaymentStatus
{
    PENDING,
    SUCCESS,
    FAILED,
    CANCELLED,
    UNKNOWN
}

public enum PaymentOrderStatus
{
    PENDING,
    PAID,
    FAILED,
    CANCELLED,
    REFUNDED
}

public enum PaymentMethod
{
    PAYOS,
    CASH,
    VNPAY,
    MOMO,
    ZALOPAY
}

public enum OrderType
{
    PARKING_FEE,
    MONTHLY_PASS_FEE,
    OVERNIGHT_FEE,
    RESERVATION_FEE
}

public enum ReferenceType
{
    RESERVATION,
    PARKING_SESSION,
    MONTHLY_PASS
}

public enum TransactionType
{
    PAYMENT,
    REFUND
}

public enum WebhookProcessingState
{
    PENDING,
    PROCESSED,
    FAILED
}
