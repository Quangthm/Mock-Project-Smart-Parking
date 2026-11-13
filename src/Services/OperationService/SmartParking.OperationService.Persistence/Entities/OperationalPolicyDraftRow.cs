namespace SmartParking.OperationService.Persistence.Entities;

public sealed class OperationalPolicyDraftRow
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public int TargetRevision { get; set; }
    public string DraftPayload { get; set; } = "{}";
    public DateTimeOffset? ScheduledActivationAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid UpdatedBy { get; set; }
}
