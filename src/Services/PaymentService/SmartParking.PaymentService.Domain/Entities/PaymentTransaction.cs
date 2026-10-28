using SmartParking.PaymentService.Domain.Enums;

namespace SmartParking.PaymentService.Domain.Entities;

public class PaymentTransaction
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PaymentOrderId { get; set; }

    public TransactionType TransactionType { get; set; } = TransactionType.PAYMENT;
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.PAYOS;
    public string? GatewayTransactionId { get; set; }
    public decimal Amount { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.PENDING;
    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public PaymentOrder? PaymentOrder { get; set; }

    public void MarkSuccess(string? gatewayTxId, DateTimeOffset paidAt)
    {
        Status = PaymentStatus.SUCCESS;
        GatewayTransactionId = gatewayTxId ?? GatewayTransactionId;
        PaidAt = paidAt;
    }

    public void MarkFailed()
    {
        if (Status == PaymentStatus.SUCCESS)
        {
            throw new InvalidOperationException("Cannot transition a SUCCESS transaction to FAILED (Immutability of Success).");
        }
        Status = PaymentStatus.FAILED;
    }

    public void MarkCancelled()
    {
        if (Status == PaymentStatus.SUCCESS)
        {
            throw new InvalidOperationException("Cannot transition a SUCCESS transaction to CANCELLED.");
        }
        Status = PaymentStatus.CANCELLED;
    }

    public void MarkUnknown()
    {
        if (Status == PaymentStatus.SUCCESS)
        {
            return;
        }
        Status = PaymentStatus.UNKNOWN;
    }
}
