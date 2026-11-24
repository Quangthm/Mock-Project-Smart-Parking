namespace SmartParking.OperationService.Domain.Entities;

// Operations mirror of a Facility emergency restriction. A closure row blocks new admissions as
// soon as it arrives; affected commitments are reconciled separately.
public sealed class StructureEmergencyClosureRow
{
    public Guid RestrictionId { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public string Mode { get; set; } = "";
    public string Scope { get; set; } = "";
    public Guid? UnitId { get; set; }
    public string AffectedSlotIds { get; set; } = "[]";
    public string Reason { get; set; } = "";
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveUntil { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }
}