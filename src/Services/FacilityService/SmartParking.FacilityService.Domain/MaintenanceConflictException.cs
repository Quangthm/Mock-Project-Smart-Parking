namespace SmartParking.FacilityService.Domain;

// A maintenance window cannot be enforced because live commitments already overlap it.
// Nothing was cancelled or moved; the caller receives the overlapping commitments so it can
// present the affected reservations instead of silently displacing them.
public sealed class MaintenanceConflictException(
    IReadOnlyList<MaintenanceCommitmentItem> commitments,
    string message) : StructureException("STRUCTURE_IN_USE", message)
{
    public IReadOnlyList<MaintenanceCommitmentItem> Commitments { get; } = commitments;
}