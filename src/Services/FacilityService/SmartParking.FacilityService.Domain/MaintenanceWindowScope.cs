namespace SmartParking.FacilityService.Domain;

// Resource snapshot Operations needs to test a maintenance window against live commitments.
// Site-wide windows resolve to empty lists; slot-scoped windows carry exactly one unit and slot.
public sealed record MaintenanceWindowScope(IReadOnlyList<Guid> UnitIds, IReadOnlyList<Guid> SlotIds);