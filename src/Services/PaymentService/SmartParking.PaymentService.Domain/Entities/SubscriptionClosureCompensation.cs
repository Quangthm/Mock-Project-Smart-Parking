namespace SmartParking.PaymentService.Domain.Entities;

public class SubscriptionClosureCompensation
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public Guid ClosureEventId { get; set; }
    public string ClosureType { get; set; } = "TEMPORARY";
    public Guid PassId { get; set; }
    public Guid PaymentOrderId { get; set; }
    public Guid? RefundRequestId { get; set; }
    public DateTimeOffset? CompensatedFrom { get; set; }
    public DateTimeOffset? CompensatedTo { get; set; }
    public IReadOnlyList<ClosureInterval> CompensatedIntervals { get; set; } = [];
    public decimal RefundAmount { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string IdempotencyPayloadHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
