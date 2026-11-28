using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;

namespace SmartParking.FacilityService.Infrastructure;

// Makes entering and leaving an acknowledged window observable to projections exactly once, by
// stamping the start/end watermarks and staging a layout invalidation for each affected site.
// The same 15s tick also auto-expires emergency restrictions whose `effective_until` has passed, so a
// timed closure never lingers in physical state after its window closes.
public sealed class MaintenanceBoundaryTickerWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<MaintenanceBoundaryTickerWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RunTickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    // One tick runs both sweeps. A transient failure in either is logged without preventing the other
    // from running on the same tick, so maintenance boundaries and expiry never block each other.
    public async Task RunTickAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();

        try
        {
            var store = scope.ServiceProvider.GetRequiredService<IMaintenanceWindowStore>();
            await store.NotifyDueBoundariesAsync(DateTime.UtcNow, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            logger.LogError(error, "Maintenance boundary tick failed; retrying on the next tick.");
        }

        try
        {
            // Optional: older deployments may not register the restriction store, and a missing store
            // must never stop the boundary sweep above.
            var emergencyStore = scope.ServiceProvider.GetService<IEmergencyRestrictionStore>();
            if (emergencyStore is not null)
                await emergencyStore.ExpireDueAsync(DateTimeOffset.UtcNow, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            logger.LogError(error, "Emergency restriction expiry tick failed; retrying on the next tick.");
        }
    }
}
