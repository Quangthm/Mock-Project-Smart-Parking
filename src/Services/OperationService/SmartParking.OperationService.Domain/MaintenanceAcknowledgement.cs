namespace SmartParking.OperationService.Domain;

/// <summary>
/// Acknowledgement returned to Facility once Operations has durably applied a
/// maintenance window (or replayed an identical request).
/// </summary>
public sealed record MaintenanceAcknowledgement(Guid EnforcementRef, DateTimeOffset AcknowledgedAt);