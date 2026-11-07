using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application;

namespace SmartParking.FacilityService.Infrastructure;

// Scoped stores/services are resolved per pass. No request identity is needed:
// the worker only replays a payload that was saved by an authorized use case.
// Live user operations automatically trigger on-demand lazy recovery during
// StructureUnitOfWork.BeginEditAsync. The background worker serves purely as a
// low-overhead 24-hour safety net for abandoned sites in mock/internship environments.
public sealed class StructureSynchronizationWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<StructureSynchronizationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = configuration.GetValue("Synchronization:RecoveryIntervalSeconds", 86400); // 1 ngày = 86400 giây
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IStructureSynchronizationService>();
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
