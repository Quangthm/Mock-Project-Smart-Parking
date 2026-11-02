using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Persistence;

namespace SmartParking.OperationService.Infrastructure.Outbox;

public sealed class OccupancyOutboxWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<OccupancyOutboxWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(60);
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
        var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IOutboxEventPublisher>();

        var batch = await db.OutboxEvents
            .FromSqlRaw(
                "SELECT * FROM outbox_events WHERE published_at IS NULL ORDER BY occurred_at, sequence_number LIMIT {0} FOR UPDATE SKIP LOCKED",
                BatchSize)
            .AsAsyncEnumerable()
            .ToListAsync(stoppingToken);
        if (batch.Count == 0) return 0;

        // The FOR UPDATE lock is meaningful only inside a transaction; keep it open for the batch.
        await using var transaction = await db.Database.BeginTransactionAsync(stoppingToken);
        batch = await db.OutboxEvents
            .FromSqlRaw(
                "SELECT * FROM outbox_events WHERE published_at IS NULL ORDER BY occurred_at, sequence_number LIMIT {0} FOR UPDATE SKIP LOCKED",
                BatchSize)
            .AsAsyncEnumerable()
            .ToListAsync(stoppingToken);

        foreach (var row in batch)
        {
            await publisher.PublishAsync(
                new OutboxMessage(row.EventId, row.EventType, row.OccurredAt, row.SequenceNumber, row.SiteId, row.Payload),
                stoppingToken);
            row.PublishedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(stoppingToken);
        await transaction.CommitAsync(stoppingToken);
        return batch.Count;
    }
}
