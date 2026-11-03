namespace SmartParking.FacilityService.Domain.Entities;

public sealed class StructureOperationEntity
{
    public Guid Id { get; set; }
    public Guid SiteId { get; set; }
    public Guid TenantId { get; set; }
    public Guid ActorId { get; set; }
    public Guid IdempotencyKey { get; set; }
    public string Request { get; set; } = "";
    public string Status { get; set; } = "pending";
    public string? Reason { get; set; }
    public string? Impact { get; set; }
    public bool PhysicalCommitted { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
