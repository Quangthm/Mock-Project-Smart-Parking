namespace SmartParking.OperationService.Domain.Entities;

public sealed class ReservationRow
{
    public void EnterParking()
    {
        if (Status is not ("CONFIRMED" or "ALLOCATED")) throw new GateTransitionException("INVALID_RESERVATION_STATE", "Only confirmed reservations can enter parking.");
        Status = "PARKING";
    }
    public void CompleteParking()
    {
        if (Status != "PARKING") throw new GateTransitionException("INVALID_RESERVATION_STATE", "Only parked reservations can depart.");
        Status = "COMPLETED";
        ActiveClaimSlotId = null;
    }

    // Historical first-confirmation capture only. This timestamp does NOT establish D4c commit evidence.
    public void RecordFirstConfirmation(DateTimeOffset observedAt)
    {
        if (ConfirmedAt is null && Status is "CONFIRMED" or "ALLOCATED" or "PARKING") ConfirmedAt = observedAt;
    }
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? TargetSpatialUnitId { get; set; }
    public Guid? TargetSlotId { get; set; }
    public Guid? ActiveClaimSlotId { get; set; }
    public int Version { get; set; } = 1;
    public string? OriginalPlate { get; set; }
    public string? NormalizedPlate { get; set; }
    public string VehicleType { get; set; } = "";
    public DateTimeOffset ExpectedStartTime { get; set; }
    public DateTimeOffset ExpectedEndTime { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public string? AcceptedOperationalPolicySnapshot { get; set; }
    public string Status { get; set; } = "";
    public DateTimeOffset? HoldExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ParkingSessionRow
{
    public void RecordDeparture(DateTimeOffset now, string? image)
    {
        if (ExitTime is not null) return;
        if (SessionStatus != "ACTIVE" || now < EntryTime) throw new GateTransitionException("INVALID_SESSION_STATE", "Only active parking sessions can depart.");
        ExitTime = now;
        ExitImageUrl = image;
        SessionStatus = "COMPLETED";
    }

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
    public void Occupy(Guid sessionId)
    {
        if (AllocationStatus != "RESERVED") throw new GateTransitionException("ALLOCATION_UNAVAILABLE", "A reserved allocation is required.");
        AllocationStatus = "OCCUPIED";
        ParkingSessionId = sessionId;
    }
    public void Release(DateTimeOffset now)
    {
        if (AllocationStatus != "OCCUPIED") throw new GateTransitionException("INVALID_ALLOCATION_STATE", "Only occupied allocations can depart.");
        AllocationStatus = "RELEASED";
        AllocatedEndTime = now;
    }

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
    public void ApplyStructureCapacity(int total, int backup, DateTimeOffset now)
    {
        if (total < 0 || backup < 0 || backup > total || CurrentReservedCount > total)
            throw new SmartParking.OperationService.Domain.InvalidStructureEditException("Capacity cannot invalidate existing commitments.");
        TotalCapacity = total;
        MaxReservationQuota = Math.Min(MaxReservationQuota, total);
        BackupCount = backup;
        UpdatedAt = now;
    }
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

public sealed class StructureReleaseReceiptRow
{
    public Guid Token { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public string Outcome { get; set; } = "";
    public DateTimeOffset CompletedAt { get; set; }
}
