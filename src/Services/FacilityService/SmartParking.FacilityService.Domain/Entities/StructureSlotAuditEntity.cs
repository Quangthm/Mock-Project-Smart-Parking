namespace SmartParking.FacilityService.Domain.Entities;

public sealed class StructureSlotAuditEntity
{
    public Guid Id { get; set; }
    public Guid SiteId { get; set; }
    public Guid SlotId { get; set; }
    public Guid ActorId { get; set; }
    public string Action { get; set; } = "";
    public string? Reason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
