using MediatR;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record ReservationAvailabilityFacts(IReadOnlyList<ReservationRow> Reservations,
    IReadOnlyList<ParkingSessionRow> Sessions, IReadOnlyList<SlotAllocationRow> Allocations,
    IReadOnlyList<OperationalCapacityConfig> Quotas);
public interface IReservationAvailabilityRepository
{
    Task<ReservationAvailabilityFacts> ReadAsync(ReservationRow proposed, DateTimeOffset evaluatedAt, CancellationToken ct);
    Task LockAccountAsync(Guid accountId, CancellationToken ct);
    Task<bool> HasUnclassifiedCancellationAsync(Guid accountId, CancellationToken ct);
    Task<IReadOnlyList<ReservationCancellationEvidence>> ReadCancellationsAsync(Guid accountId, DateTimeOffset dayStart, DateTimeOffset now, CancellationToken ct);
}
public sealed record RequireReservationAvailabilityCommand(ReservationRow Proposed,
    FacilityResourceSnapshot Resources, DateTimeOffset EvaluatedAt) : IRequest;
public sealed record RequireBookingPermissionCommand(Guid AccountId, EffectiveReservationPolicy Policy,
    DateTimeOffset EvaluatedAt) : IRequest;
