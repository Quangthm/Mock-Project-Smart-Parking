namespace SmartParking.FacilityService.Domain.Entities;

public sealed class StructureSynchronizationEntity
{
    public Guid Token { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public Guid? OperationId { get; set; }
    public string State { get; set; } = "";
    public string? Outcome { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
