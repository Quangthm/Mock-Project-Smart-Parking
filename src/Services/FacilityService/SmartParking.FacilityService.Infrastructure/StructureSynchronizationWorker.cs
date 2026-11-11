using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application;
using MediatR;

namespace SmartParking.FacilityService.Infrastructure;

// Scoped handlers/repositories are resolved per pass. No request identity is needed:
// the worker only replays a payload that was saved by an authorized use case.
// Live user operations automatically trigger on-demand lazy recovery during
// BeginStructureEditCommand. The background worker serves purely as a
// recovery path for abandoned sites; reservation deadlines require a short cadence.
public sealed class StructureSynchronizationWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<StructureSynchronizationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = configuration.GetValue("Synchronization:RecoveryIntervalSeconds", 1);
        if (intervalSeconds <= 0)
            throw new InvalidOperationException("Synchronization recovery interval must be positive.");
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var failures = await mediator.Send(new RecoverPendingStructuresCommand(), stoppingToken);
                foreach (var failure in failures)
                {
                    logger.LogWarning("Structure synchronization will be retried ({FailureType}).", failure.Error.GetType().Name);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error)
            {
                // Leave the durable row unchanged; the next pass retries.
                logger.LogWarning("Structure synchronization failed and will be retried ({FailureType}).", error.GetType().Name);
            }
        }
    }
}
