namespace SmartParking.OperationService.Domain.Entities;

/// <summary>
/// Durable record of a planned maintenance closure that Operations acknowledged.
/// A closure never cancels or moves commitments; it only blocks new admissions
/// whose period overlaps the closed window.
/// </summary>
public sealed class StructureMaintenanceClosureRow
{
    public Guid WindowId { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public Guid? UnitId { get; set; }
    public Guid? SlotId { get; set; }
    public string? ScopedUnitIds { get; set; }
    public string? ScopedSlotIds { get; set; }
    public DateTimeOffset WindowStart { get; set; }
    public DateTimeOffset WindowEnd { get; set; }
    public string Reason { get; set; } = "";
    public Guid EnforcementRef { get; set; }
    public DateTimeOffset AcknowledgedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
}