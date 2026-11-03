namespace SmartParking.FacilityService.Domain.Entities;

public sealed class StructureBackupPolicyEntity
{
    public Guid Id { get; set; }
    public Guid SiteId { get; set; }
    public Guid? UnitId { get; set; }
    public string VehicleType { get; set; } = "";
    public int Count { get; set; }
}
