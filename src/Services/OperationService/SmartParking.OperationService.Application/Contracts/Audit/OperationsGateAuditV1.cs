namespace SmartParking.Contracts.Audit;

// Producer: Operations. Consumer/retention owner: Audit service.
public sealed record OperationsGateAuditV1(Guid EventId, DateTimeOffset OccurredAt, Guid ActorUserId,
    Guid TenantId, Guid SiteId, string Action, Guid? ReservationId, Guid ParkingSessionId);

public static class OperationsAuditTopology
{
    public const string Exchange = "smartpark.operations.gate-audit.v1";
    public const string Queue = "smartpark.notification-audit.operations-gate.v1";
}
