namespace SmartParking.PaymentService.Domain.Entities;

public class SubscriptionFinancialTermRevision
{
    public Guid Id { get; set; }
    public Guid FinancialTermId { get; set; }
    public Guid PlanId { get; set; }
    public int PlanRevision { get; set; }
    public int RevisionNumber { get; set; }
    public IReadOnlyList<SubscriptionVehiclePrice> VehiclePrices { get; set; } = [];
    public SubscriptionRefundPolicy RefundPolicy { get; set; } = SubscriptionRefundPolicy.M05Default;
    public DateTimeOffset EffectiveFrom { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EffectiveTo { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? IdempotencyPayloadHash { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public SubscriptionFinancialTerm? FinancialTerm { get; set; }
}
