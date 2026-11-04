namespace SmartParking.FacilityService.Domain.Entities;

public sealed class OperatorAuditLogEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public Guid? ActorId { get; set; }
    public string ActorRole { get; set; } = "operator";
    public string Action { get; set; } = "";
    public string TargetEntity { get; set; } = "OPERATOR_OPERATION";
    public Guid? TargetId { get; set; }
    public string Reason { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
