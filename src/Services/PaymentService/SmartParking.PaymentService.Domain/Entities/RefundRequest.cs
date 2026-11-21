namespace SmartParking.PaymentService.Domain.Entities;

public class RefundRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid PaymentOrderId { get; set; }
    public Guid? CollectionId { get; init; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? EvidenceUrl { get; set; }
    public string Status { get; set; } = "REFUND_REQUESTED";
    public Guid? ApprovedBy { get; set; }
    public bool HandledByOwner { get; set; } = false;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public PaymentOrder? PaymentOrder { get; set; }

    public static RefundRequest ForVerifiedCollection(PaymentTransaction collection, string reason)
    {
        if (!collection.HasVerifiedProviderCollection())
            throw new InvalidOperationException("A collection disposition requires verified payment evidence.");

        return new RefundRequest
        {
            CollectionId = collection.Id,
            PaymentOrderId = collection.PaymentOrderId,
            TenantId = collection.TenantId,
            Amount = collection.Amount,
            Reason = reason
        };
    }
}
