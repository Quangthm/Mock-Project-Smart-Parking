namespace SmartParking.FacilityService.Domain.Entities;

public sealed class SpatialUnitEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public Guid? ParentId { get; set; }
    public string Path { get; set; } = "";
    public string UnitType { get; set; } = "";
    public string Name { get; set; } = "";
    public int MaxCapacity { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
