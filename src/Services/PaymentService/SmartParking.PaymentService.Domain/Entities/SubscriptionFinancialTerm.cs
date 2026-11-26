namespace SmartParking.PaymentService.Domain.Entities;

public class SubscriptionFinancialTerm
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public Guid PlanId { get; set; }
    public string ProductType { get; set; } = "MONTHLY";
    public int ActiveRevision { get; set; } = 1;
    public string Currency { get; set; } = "VND";
    public bool IsActive { get; set; } = true;
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<SubscriptionFinancialTermRevision> Revisions { get; set; } =
        new List<SubscriptionFinancialTermRevision>();
}
