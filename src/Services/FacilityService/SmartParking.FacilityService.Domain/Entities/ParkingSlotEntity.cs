namespace SmartParking.FacilityService.Domain.Entities;

public sealed class ParkingSlotEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public Guid SpatialUnitId { get; set; }
    public string SlotCode { get; set; } = "";
    public string SupportedVehicleType { get; set; } = "";
    public string SlotType { get; set; } = "";
    public string PhysicalState { get; set; } = "AVAILABLE";
    public string? Coordinates3D { get; set; }
    public string? Features { get; set; }
    public string? ReservationState { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
