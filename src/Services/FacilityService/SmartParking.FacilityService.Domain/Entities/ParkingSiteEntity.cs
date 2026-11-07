namespace SmartParking.FacilityService.Domain.Entities;

public sealed class ParkingSiteEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public DateTimeOffset? DeletedAt { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public int TotalPhysicalCapacity { get; set; }
}
