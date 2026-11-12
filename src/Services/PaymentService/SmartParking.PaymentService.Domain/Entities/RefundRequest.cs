namespace SmartParking.PaymentService.Domain.Entities;

public class RefundRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid PaymentOrderId { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? EvidenceUrl { get; set; }
    public string Status { get; set; } = "REFUND_REQUESTED";
    public Guid? ApprovedBy { get; set; }
    public bool HandledByOwner { get; set; } = false;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public PaymentOrder? PaymentOrder { get; set; }
}
