using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

public interface IReservationAvailabilityRepository
{
    Task<ReservationAvailabilityFacts> ReadAsync(ReservationRow proposed, DateTimeOffset evaluatedAt, CancellationToken ct);
    Task LockAccountAsync(Guid accountId, CancellationToken ct);
    Task<bool> HasUnclassifiedCancellationAsync(Guid accountId, CancellationToken ct);
    Task<IReadOnlyList<ReservationCancellationEvidence>> ReadCancellationsAsync(Guid accountId, DateTimeOffset dayStart, DateTimeOffset now, CancellationToken ct);
}
