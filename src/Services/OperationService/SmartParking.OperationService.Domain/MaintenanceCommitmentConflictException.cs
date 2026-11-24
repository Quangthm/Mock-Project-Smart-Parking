namespace SmartParking.OperationService.Domain;

/// <summary>
/// Raised when a requested maintenance window overlaps one or more live
/// reservations or sessions. DE-4: they are never cancelled or moved.
/// </summary>
public sealed class MaintenanceCommitmentConflictException(
    IReadOnlyList<MaintenanceCommitmentConflict> commitments,
    string message) : Exception(message)
{
    public IReadOnlyList<MaintenanceCommitmentConflict> Commitments { get; } = commitments;
}