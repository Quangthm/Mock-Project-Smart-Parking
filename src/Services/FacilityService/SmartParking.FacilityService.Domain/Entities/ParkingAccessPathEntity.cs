namespace SmartParking.FacilityService.Domain.Entities;

public sealed class ParkingAccessPathEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public string Code { get; set; } = "";
    public Guid? FromUnitId { get; set; }
    public Guid? ToUnitId { get; set; }
    public string? MapData { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
