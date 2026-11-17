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
