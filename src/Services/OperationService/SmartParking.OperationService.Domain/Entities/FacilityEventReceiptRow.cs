namespace SmartParking.OperationService.Domain.Entities;

// Idempotency ledger for inbound Facility integration events. The row is written in the same
// transaction as the reconciliation result so a redelivery can be skipped atomically.
public sealed class FacilityEventReceiptRow
{
    public Guid EventId { get; set; }
    public string EventType { get; set; } = "";
    public Guid SiteId { get; set; }
    public DateTimeOffset ProcessedAt { get; set; }
}