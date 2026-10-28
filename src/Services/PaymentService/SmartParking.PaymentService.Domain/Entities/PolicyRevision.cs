namespace SmartParking.PaymentService.Domain.Entities;

public class PolicyRevision
{
    public Guid Id { get; set; }
    public Guid PolicyId { get; set; }
    public string PolicyType { get; set; } = "TARIFF";
    public int RevisionNumber { get; set; } = 1;
    public DateTimeOffset EffectiveFrom { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EffectiveTo { get; set; }
    public string ScopeType { get; set; } = "SITE";
    public Guid? ScopeId { get; set; }
    public string PolicyValuesJson { get; set; } = "{}";
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
