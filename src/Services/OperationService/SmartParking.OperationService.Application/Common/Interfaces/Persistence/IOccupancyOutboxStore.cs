using SmartParking.OperationService.Application.Common.Interfaces.Services;

namespace SmartParking.OperationService.Application.Common.Interfaces.Persistence;

/// <summary>
/// Claim/lease port for the transactional outbox. The claim must be an atomic,
/// short-lived statement that releases its row locks before the caller performs
/// any network I/O; confirmation is a separate short statement.
/// </summary>
public interface IOccupancyOutboxStore
{
    Task<IReadOnlyList<OutboxMessage>> ClaimBatchAsync(
        int batchSize,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken);

    Task MarkPublishedAsync(
        IReadOnlyCollection<Guid> eventIds,
        CancellationToken cancellationToken);
}