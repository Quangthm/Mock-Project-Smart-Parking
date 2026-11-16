namespace SmartParking.OperationService.Domain.Entities;

public sealed partial class ReservationRow
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
