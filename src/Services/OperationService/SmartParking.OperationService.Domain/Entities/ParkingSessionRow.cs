namespace SmartParking.OperationService.Domain.Entities;

public sealed class ParkingSessionRow
{
    public void AwaitCheckoutPayment(Guid orderId)
    {
        if (ExitTime is not null || SessionStatus is not ("ACTIVE" or "PENDING_PAYMENT"))
            throw new GateTransitionException("INVALID_SESSION_STATE", "Only open sessions can await checkout payment.");
        CheckoutPaymentOrderId = orderId;
        SessionStatus = "PENDING_PAYMENT";
    }

    public bool RecordCheckoutSettlement(Guid orderId, DateTimeOffset occurredAt)
    {
        if (CheckoutPaymentOrderId != orderId || occurredAt < EntryTime)
            return false;
        CheckoutPaidAt ??= occurredAt;
        return true;
    }

    public void RecordSlotScan(Guid slotId, DateTimeOffset scannedAt)
    {
        if (ExitTime is not null)
            throw new GateTransitionException("INVALID_SESSION_STATE", "Only open sessions can record a slot scan.");
        CurrentSlotId = slotId;
        LocationScannedAt = scannedAt;
    }

    public void RecordDeparture(DateTimeOffset now, string? image)
    {
        if (ExitTime is not null) return;
        if (SessionStatus is not ("ACTIVE" or "PENDING_PAYMENT") || now < EntryTime) throw new GateTransitionException("INVALID_SESSION_STATE", "Only active parking sessions can depart.");
        ExitTime = now;
        ExitImageUrl = image ?? ExitImageUrl;
        SessionStatus = "COMPLETED";
    }

    public void CaptureCheckoutImage(string? image)
    {
        if (string.IsNullOrWhiteSpace(image)) return;
        if (ExitTime is not null || SessionStatus is not ("ACTIVE" or "PENDING_PAYMENT"))
            throw new GateTransitionException("INVALID_SESSION_STATE", "Only an open parking session can capture checkout evidence.");
        ExitImageUrl = image;
    }

    public Guid Id { get; set; }
    public Guid? ReservationId { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public Guid? AccessItemId { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? CurrentSlotId { get; set; }
    public Guid? CurrentSpatialUnitId { get; set; }
    public string? OriginalPlate { get; set; }
    public string? NormalizedPlate { get; set; }
    public string VehicleType { get; set; } = "";
    public string? TicketReference { get; set; }
    public DateTimeOffset EntryTime { get; set; }
    public string? EntryImageUrl { get; set; }
    public DateTimeOffset? ExitTime { get; set; }
    public string? ExitImageUrl { get; set; }
    public DateTimeOffset? LocationScannedAt { get; set; }
    public Guid? CheckoutPaymentOrderId { get; private set; }
    public DateTimeOffset? CheckoutPaidAt { get; private set; }
    public string SessionStatus { get; set; } = "";
    public int Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
