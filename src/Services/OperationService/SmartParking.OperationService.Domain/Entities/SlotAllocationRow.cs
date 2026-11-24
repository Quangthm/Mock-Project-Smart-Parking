namespace SmartParking.OperationService.Domain.Entities;

public sealed class SlotAllocationRow
{
    public void AttachToSession(Guid sessionId)
    {
        if (AllocationStatus != "RESERVED" || ParkingSessionId is not null && ParkingSessionId != sessionId)
            throw new GateTransitionException("ALLOCATION_UNAVAILABLE", "A reserved allocation is required.");
        ParkingSessionId = sessionId;
    }

    public void Occupy(Guid sessionId)
    {
        AttachToSession(sessionId);
        AllocationStatus = "OCCUPIED";
    }
    // A reserved allocation follows its booking to a replacement bay. An occupied allocation is
    // pinned to the vehicle that is physically standing there and can never be moved.
    public void MoveTo(Guid slotId)
    {
        if (slotId == Guid.Empty)
            throw new GateTransitionException("INVALID_ALLOCATION_STATE", "A replacement bay is required.");
        if (AllocationStatus != "RESERVED")
            throw new GateTransitionException("INVALID_ALLOCATION_STATE", "Only a reserved allocation can be moved.");
        SlotId = slotId;
    }

    public void Release(DateTimeOffset now)
    {
        if (AllocationStatus is not ("OCCUPIED" or "RESERVED"))
            throw new GateTransitionException("INVALID_ALLOCATION_STATE", "Only active allocations can be released.");
        AllocationStatus = "RELEASED";
        if (now > AllocatedStartTime)
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
