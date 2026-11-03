namespace SmartParking.FacilityService.Domain.Entities;

public sealed class OperatorIncidentEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public Guid ActorId { get; set; }
    public string Type { get; set; } = "";
    public string Details { get; set; } = "";
    public string Status { get; set; } = "reported";
    public DateTimeOffset CreatedAt { get; set; }
}
