using MediatR;
using SmartParking.FacilityService.Domain.Entities;

namespace SmartParking.FacilityService.Application.Reservations;

// Version 1 wire DTOs are local to this service; no cross-service assembly reference.
public sealed record DependencyIntent(Guid Token, Guid WorkflowId, string Purpose, Guid TenantId,
    Guid SiteId, Guid SlotId, string IntentFingerprint, DateTimeOffset Deadline);
public sealed record BookingFacts(Guid TenantId, Guid SiteId, Guid SlotId, string Version,
    bool EligibleCarSlot, bool ParticipatesInStructureGuard);
public sealed record ResourceFact(Guid SlotId, Guid? SpatialUnitId, string VehicleType,
    bool Active, bool Reachable, bool FixedBackup, IReadOnlyList<Guid> AncestorUnitIds,
    IReadOnlyList<string> CompatibleVehicleTypes);
public sealed record BackupFact(Guid? SpatialUnitId, string VehicleType, int Count);
public sealed record ResourceSnapshot(BookingFacts Booking, IReadOnlyList<ResourceFact> Resources,
    IReadOnlyList<BackupFact> DynamicBackups, bool Complete);
public sealed record DependencyResult(DependencyIntent AcceptedIntent, string State, ResourceSnapshot? Snapshot);
public sealed record DependencyAcknowledgement(DependencyIntent AcceptedIntent, string State, string? OutcomeFingerprint);
public sealed record AcquireDependencyCommand(DependencyIntent Intent) : IRequest<DependencyResult>;
public sealed record CompleteDependencyCommand(DependencyIntent Intent, string OutcomeJson) : IRequest<DependencyAcknowledgement>;
public sealed record AbortDependencyCommand(DependencyIntent Intent) : IRequest<DependencyAcknowledgement>;
public sealed record ReadBookingFactsQuery(Guid SiteId, Guid SlotId) : IRequest<BookingFacts>;

public interface IReservationDependencyRepository
{
    Task LockAsync(Guid token, Guid siteId, CancellationToken ct);
    Task<ReservationDependency?> FindAsync(Guid token, CancellationToken ct);
    Task<bool> HasHeldAsync(Guid siteId, CancellationToken ct);
    Task<bool> HasPendingStructureEditAsync(Guid siteId, CancellationToken ct);
    Task<ResourceSnapshot> SnapshotAsync(Guid siteId, Guid slotId, CancellationToken ct);
    Task<DateTimeOffset> NowAsync(CancellationToken ct);
    void Add(ReservationDependency dependency);
}
