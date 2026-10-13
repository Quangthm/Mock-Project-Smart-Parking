using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartParking.ParkingService.Application;

namespace SmartParking.ParkingService.Infrastructure;

// Scoped stores/services are resolved per pass. No request identity is needed:
// the worker only replays a payload that was saved by an authorized use case.
public sealed class StructureSynchronizationWorker(
    IServiceScopeFactory scopeFactory, ILogger<StructureSynchronizationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<StructureSynchronizationService>();
                var failures = await service.RecoverPendingAsync(stoppingToken);
                foreach (var failure in failures)
                {
                    logger.LogWarning(failure.Error,
                        "Structure synchronization for site {SiteId} will be retried.", failure.SiteId);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error)
            {
                // Leave the durable row unchanged; the next pass retries.
                logger.LogWarning(error, "Structure synchronization failed and will be retried.");
            }
        }
    }
}
