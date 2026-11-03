namespace SmartParking.FacilityService.Domain.Entities;

public sealed class TenantEntity
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Status { get; set; } = "ACTIVE";
    public DateTimeOffset? DeletedAt { get; set; }
}

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
    public Guid IdempotencyKey { get; set; }
    public string Request { get; set; } = "";
    public string Status { get; set; } = "pending";
    public string? Reason { get; set; }
    public string? Impact { get; set; }
    public bool PhysicalCommitted { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

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

public sealed class StructureChangeAuditEntity
{
    public Guid Id { get; set; }
    public Guid SiteId { get; set; }
    public Guid ActorId { get; set; }
    public Guid? OperationId { get; set; }
    public string? BeforeState { get; set; }
    public string? AfterState { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

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

public sealed class SupportTicketEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public Guid UserId { get; set; }
    public string Subject { get; set; } = "";
    public string Message { get; set; } = "";
    public Guid? BookingId { get; set; }
    public string Status { get; set; } = "open";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class OperatorIncidentEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public Guid ActorId { get; set; }
    public string Type { get; set; } = "";
    public string Details { get; set; } = "";
    public string Status { get; set; } = "reported";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class OperatorNotificationEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public Guid RecipientId { get; set; }
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public Guid? RelatedEntityId { get; set; }
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class OperatorAuditLogEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public Guid? ActorId { get; set; }
    public string ActorRole { get; set; } = "operator";
    public string Action { get; set; } = "";
    public string TargetEntity { get; set; } = "OPERATOR_OPERATION";
    public Guid? TargetId { get; set; }
    public string Reason { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
