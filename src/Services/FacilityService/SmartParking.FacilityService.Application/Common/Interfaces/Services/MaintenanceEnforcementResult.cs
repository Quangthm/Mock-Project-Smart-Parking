using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Common.Interfaces.Services;

// Outcome of asking Operations to enforce a planned maintenance window.
// Only `acknowledged` makes a window effective; the other outcomes leave it visible but not in force.
public abstract record MaintenanceEnforcementResult
{
    public sealed record Acknowledged(Guid EnforcementRef, DateTime AcknowledgedAt) : MaintenanceEnforcementResult;

    public sealed record Conflict(IReadOnlyList<MaintenanceCommitmentItem> Commitments, string Message)
        : MaintenanceEnforcementResult;

    public sealed record Unavailable : MaintenanceEnforcementResult;
}