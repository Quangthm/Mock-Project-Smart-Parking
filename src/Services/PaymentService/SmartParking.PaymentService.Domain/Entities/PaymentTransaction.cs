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
    public string? ProviderCollectionReference { get; private set; }
    public DateTimeOffset? ProviderVerifiedAt { get; private set; }
    public string? IdempotencyKey { get; set; }
    public decimal Amount { get; set; }

    public string? CheckoutUrl { get; set; }
    public string? QrCode { get; set; }
    public string? AccountNumber { get; set; }
    public string? AccountName { get; set; }
    public string? Bin { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.PENDING;
    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public PaymentOrder? PaymentOrder { get; set; }

    public bool HasVerifiedProviderCollection() =>
        TransactionType == TransactionType.PAYMENT && Status == PaymentStatus.SUCCESS && Amount > 0 &&
        !string.IsNullOrWhiteSpace(ProviderCollectionReference) &&
        ProviderVerifiedAt is { } verifiedAt && verifiedAt != default && PaidAt is { } paidAt && paidAt != default;

    public void RecordVerifiedProviderCollection(string reference, decimal amount, DateTimeOffset occurredAt, DateTimeOffset verifiedAt)
    {
        if (ProviderCollectionReference is not null || Status == PaymentStatus.SUCCESS ||
            TransactionType != TransactionType.PAYMENT || string.IsNullOrWhiteSpace(reference) ||
            amount <= 0 || occurredAt == default || verifiedAt == default)
            throw new InvalidOperationException("Expected an unrecorded payment with complete provider evidence.");

        ProviderCollectionReference = reference;
        ProviderVerifiedAt = verifiedAt;
        Amount = amount;
        MarkSuccess(null, occurredAt);
    }

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
