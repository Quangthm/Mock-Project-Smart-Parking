namespace SmartParking.ParkingService.Persistence.Entities;

public sealed class TenantEntity
{
    public Guid Id { get; set; }
    public string Status { get; set; } = "";
    public DateTimeOffset? DeletedAt { get; set; }
}

public sealed class ParkingSiteEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string SiteCode { get; set; } = "";
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public int TotalPhysicalCapacity { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

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

public sealed class ParkingAccessPathEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public string PathCode { get; set; } = "";
    public Guid? FromUnitId { get; set; }
    public Guid? ToUnitId { get; set; }
    public string? MapData { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

public sealed class StructureBackupPolicyEntity
{
    public Guid Id { get; set; }
    public Guid SiteId { get; set; }
    public Guid? UnitId { get; set; }
    public string VehicleType { get; set; } = "";
    public int Count { get; set; }
}

public sealed class StructureOperationEntity
{
    public Guid Id { get; set; }
    public Guid SiteId { get; set; }
    public Guid TenantId { get; set; }
    public Guid ActorId { get; set; }
    public bool PhysicalCommitted { get; set; }
}

public sealed class StructureSynchronizationEntity
{
    public Guid Token { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public string State { get; set; } = "";
    public string? Outcome { get; set; }
}

public sealed class StructureChangeAuditEntity
{
    public Guid Id { get; set; }
    public Guid SiteId { get; set; }
    public Guid ActorId { get; set; }
    public Guid? OperationId { get; set; }
    public string? BeforeState { get; set; }
    public string? AfterState { get; set; }
}

public sealed class StructureSlotAuditEntity
{
    public Guid Id { get; set; }
    public Guid SiteId { get; set; }
    public Guid SlotId { get; set; }
    public Guid ActorId { get; set; }
    public string Action { get; set; } = "";
    public string Reason { get; set; } = "";
}
