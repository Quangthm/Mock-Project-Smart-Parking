namespace SmartParking.OperationService.Domain.Entities;

/// <summary>
/// Durable local projection of a Facility-owned maintenance closure. A closure never
/// cancels bookings directly during Facility's write; Facility emits FacilityMaintenanceChangedV1
/// and Operations updates this projection under the site lock, blocks new admissions, and
/// reconciles affected commitments asynchronously.
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
    public DateTimeOffset? WindowEnd { get; set; }
    public string Reason { get; set; } = "";
    public Guid EnforcementRef { get; set; }
    public DateTimeOffset AcknowledgedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public long Revision { get; set; } = 1;
}
