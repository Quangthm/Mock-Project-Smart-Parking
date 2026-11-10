using MediatR;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record FacilityDependencyIntent(Guid Token, Guid WorkflowId, string Purpose,
    Guid TenantId, Guid SiteId, Guid SlotId, string IntentFingerprint, DateTimeOffset Deadline);
public sealed record FacilityResourceFact(Guid SlotId, Guid? SpatialUnitId, string VehicleType,
    bool Active, bool Reachable, bool FixedBackup);
public sealed record FacilityDynamicBackupFact(Guid? SpatialUnitId, string VehicleType, int Count);
public sealed record FacilityResourceSnapshot(FacilityBookingFacts Booking,
    IReadOnlyList<FacilityResourceFact> Resources, IReadOnlyList<FacilityDynamicBackupFact> DynamicBackups,
    bool Complete);
public sealed record FacilityDependencyResult(FacilityDependencyIntent AcceptedIntent, string State,
    FacilityResourceSnapshot? Snapshot);
public sealed record FacilityDependencyAcknowledgement(FacilityDependencyIntent AcceptedIntent,
    string State, string? OutcomeFingerprint);

// Infrastructure must authenticate with the dedicated Operations capability, never the browser token
// or the unrelated User binding shared key. Owner implementations remain outside this service.
public interface IReservationFacilityDependencyOwner
{
    // HELD binds immutable complete resource facts and prevents incompatible owner mutations.
    // UNKNOWN is inconclusive. ABORTED is a durable tombstone and forbids delayed acquisition.
    Task<FacilityDependencyResult> AcquireAsync(FacilityDependencyIntent intent, CancellationToken ct);
    // The owner echoes SHA256 of the exact persisted outcome bytes for completion replay.
    Task<FacilityDependencyAcknowledgement> CompleteAsync(FacilityDependencyIntent intent, string outcomeJson, CancellationToken ct);
    // Token-specific durable abort fence; must win against a delayed acquire for this token.
    Task<FacilityDependencyAcknowledgement> AbortAsync(FacilityDependencyIntent intent, CancellationToken ct);
}

public interface IReservationFacilityDependencyRepository
{
    void RequireNoTransaction();
    Task<ReservationFacilityDependency?> FindAsync(Guid token, CancellationToken ct);
    Task<ReservationFacilityDependency?> FindByWorkflowPurposeAsync(Guid workflowId, string purpose, CancellationToken ct);
    Task<ReservationFacilityDependency?> LockAsync(Guid token, CancellationToken ct);
    Task<IReadOnlyList<Guid>> DueAsync(DateTimeOffset now, int take, CancellationToken ct);
    void Add(ReservationFacilityDependency dependency);
}

// Internal worker commands, not user-selectable HTTP endpoints. Workflow handlers create/fence
// dependencies under their own row guard; unique (workflow_id,purpose) prevents duplicate tokens.
public sealed record AdvanceFacilityDependencyCommand(Guid Token) : IRequest;
public sealed record ScheduleFacilityDependencyRetryCommand(Guid Token) : IRequest;
