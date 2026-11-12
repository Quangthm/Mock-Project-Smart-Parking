using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Interfaces.Services;

namespace SmartParking.OperationService.Infrastructure.Outbox;

public sealed class OccupancyOutboxWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<OccupancyOutboxWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(30);
    private const int BatchSize = 100;

    private TimeSpan _backoff = TimeSpan.Zero;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            int published;
            try
            {
                published = await PublishBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _backoff = _backoff == TimeSpan.Zero
                    ? TimeSpan.FromSeconds(1)
                    : TimeSpan.FromTicks(Math.Min(_backoff.Ticks * 2, MaxBackoff.Ticks));
                logger.LogWarning("Occupancy outbox publish failed ({FailureType}); retrying in {BackoffSeconds}s.",
                    ex.GetType().Name, (int)_backoff.TotalSeconds);
                await Task.Delay(_backoff, stoppingToken);
                continue;
            }

            if (published > 0)
            {
                _backoff = TimeSpan.Zero;
                continue;
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task<int> PublishBatchAsync(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IOccupancyOutboxStore>();
        var publisher = scope.ServiceProvider.GetRequiredService<IOutboxEventPublisher>();
        var broadcaster = scope.ServiceProvider.GetService<IRealtimeLayoutBroadcaster>();

        // Claim is a single short statement; no DB transaction/lock spans the network calls below.
        var batch = await store.ClaimBatchAsync(BatchSize, LeaseDuration, stoppingToken);
        if (batch.Count == 0) return 0;

        var publishedIds = new List<Guid>(batch.Count);
        foreach (var message in batch)
        {
            // A publisher failure propagates and leaves the message unconfirmed; the lease
            // expires so the row becomes claimable again for retry (at-least-once delivery).
            await publisher.PublishAsync(message, stoppingToken);
            if (broadcaster is not null)
            {
                try
                {
                    await broadcaster.BroadcastAsync(message, stoppingToken);
                }
                catch (Exception ex)
                {
                    // A failed broadcast must not block the outbox: the broker message is
                    // already published and clients recover via SnapshotRequired on reconnect.
                    logger.LogWarning(ex, "Realtime broadcast failed for event {EventId}.", message.EventId);
                }
            }
            publishedIds.Add(message.EventId);
        }

        // Confirm in a second short statement, after all network I/O has completed.
        await store.MarkPublishedAsync(publishedIds, stoppingToken);
        return batch.Count;
    }
}
