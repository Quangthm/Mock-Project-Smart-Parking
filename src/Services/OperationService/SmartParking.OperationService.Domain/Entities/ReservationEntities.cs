namespace SmartParking.OperationService.Domain.Entities;

public sealed class ReservationRow
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? TargetSpatialUnitId { get; set; }
    public Guid? TargetSlotId { get; set; }
    public string? OriginalPlate { get; set; }
    public string? NormalizedPlate { get; set; }
    public string VehicleType { get; set; } = "";
    public DateTimeOffset ExpectedStartTime { get; set; }
    public DateTimeOffset ExpectedEndTime { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public string Status { get; set; } = "";
    public DateTimeOffset? HoldExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ParkingSessionRow
{
    public Guid Id { get; set; }
    public Guid? ReservationId { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public Guid? AccessItemId { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? CurrentSlotId { get; set; }
    public string? OriginalPlate { get; set; }
    public string? NormalizedPlate { get; set; }
    public string VehicleType { get; set; } = "";
    public string? TicketReference { get; set; }
    public DateTimeOffset EntryTime { get; set; }
    public string? EntryImageUrl { get; set; }
    public DateTimeOffset? ExitTime { get; set; }
    public string? ExitImageUrl { get; set; }
    public string SessionStatus { get; set; } = "";
    public int Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class SlotAllocationRow
{
    public Guid Id { get; set; }
    public Guid? ReservationId { get; set; }
    public Guid? ParkingSessionId { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public Guid SlotId { get; set; }
    public DateTimeOffset AllocatedStartTime { get; set; }
    public DateTimeOffset AllocatedEndTime { get; set; }
    public string AllocationStatus { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class SiteCapacityPoolRow
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public Guid? SpatialUnitId { get; set; }
    public string VehicleType { get; set; } = "";
    public int TotalCapacity { get; set; }
    public int MaxReservationQuota { get; set; }
    public int CurrentReservedCount { get; set; }
    public int PendingPaymentCount { get; set; }
    public int OccupiedCount { get; set; }
    public int ProtectedCount { get; set; }
    public int BackupCount { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class StructureEditHoldRow
{
    public Guid SiteId { get; set; }
    public Guid TenantId { get; set; }
    public Guid Token { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class StructureSiteStateRow
{
    public Guid SiteId { get; set; }
    public Guid TenantId { get; set; }
    public bool Active { get; set; }
}

public sealed class StructureRemovedResourceRow
{
    public Guid Id { get; set; }
    public Guid SiteId { get; set; }
    public string Kind { get; set; } = "";
}

public sealed class StructureReleaseReceiptRow
{
    public Guid Token { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public string Outcome { get; set; } = "";
    public DateTimeOffset CompletedAt { get; set; }
}

public sealed class AuditLogRow
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public Guid ActorId { get; set; }
    public string ActorRole { get; set; } = "";
    public string Action { get; set; } = "";
    public string TargetEntity { get; set; } = "";
    public Guid TargetId { get; set; }
    public string Reason { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
