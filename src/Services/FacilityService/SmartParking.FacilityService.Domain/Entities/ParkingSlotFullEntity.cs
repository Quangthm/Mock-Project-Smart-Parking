namespace SmartParking.FacilityService.Domain.Entities;

public sealed class ParkingSlotFullEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public Guid SpatialUnitId { get; set; }
    public string Code { get; set; } = "";
    public string VehicleType { get; set; } = "";
    public string SlotType { get; set; } = "";
    public string PhysicalState { get; set; } = "";
    public string? Coordinates3D { get; set; }
    public string? Features { get; set; }
    public string? ReservationState { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
