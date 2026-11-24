using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Tests.TestDoubles;

// Availability port double: returns a fixed fact set and records account guards. The real guard
// requires a database transaction, which the emergency handler tests do not need.
internal sealed class StubAvailabilityRepository(ReservationAvailabilityFacts facts)
    : IReservationAvailabilityRepository
{
    public List<Guid> LockedAccounts { get; } = [];

    public Task<ReservationAvailabilityFacts> ReadAsync(
        ReservationRow proposed, DateTimeOffset evaluatedAt, CancellationToken ct) =>
        Task.FromResult(facts);

    public Task LockAccountAsync(Guid accountId, CancellationToken ct)
    {
        LockedAccounts.Add(accountId);
        return Task.CompletedTask;
    }

    public Task<bool> HasUnclassifiedCancellationAsync(Guid accountId, CancellationToken ct) =>
        Task.FromResult(false);

    public Task<IReadOnlyList<ReservationCancellationEvidence>> ReadCancellationsAsync(
        Guid accountId, DateTimeOffset dayStart, DateTimeOffset now, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ReservationCancellationEvidence>>([]);
}