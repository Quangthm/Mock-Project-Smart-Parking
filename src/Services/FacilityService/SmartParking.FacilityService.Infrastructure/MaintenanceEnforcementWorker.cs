using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Common.Interfaces.Services;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Infrastructure;

// Retries windows that were committed `pending` because Operations was unreachable when the Owner
// created them, and releases cancellations that could not be delivered at cancel time.
public sealed class MaintenanceEnforcementWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<MaintenanceEnforcementWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<IMaintenanceWindowStore>();
                var enforcement = scope.ServiceProvider.GetRequiredService<IMaintenanceEnforcementClient>();

                foreach (var window in await store.ListPendingEnforcementAsync(DateTime.UtcNow, stoppingToken))
                    await ApplyAsync(store, enforcement, window, stoppingToken);

                foreach (var window in await store.ListCancelledPendingRemoteSyncAsync(stoppingToken))
                {
                    if (await enforcement.CancelAsync(window.TenantId, window.SiteId, window.Id, stoppingToken))
                        await store.ClearEnforcementRefAsync(window.TenantId, window.SiteId, window.Id, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error)
            {
                logger.LogError(error, "Maintenance enforcement sweep failed; retrying on the next tick.");
            }
        }
    }

    private async Task ApplyAsync(
        IMaintenanceWindowStore store,
        IMaintenanceEnforcementClient enforcement,
        MaintenanceWindow window,
        CancellationToken ct)
    {
        var scope = await store.ResolveScopeAsync(window.TenantId, window.SiteId, window.UnitId, window.SlotId, ct);
        var result = await enforcement.ApplyAsync(window, scope, ct);
        switch (result)
        {
            case MaintenanceEnforcementResult.Acknowledged acknowledged:
                await store.MarkAcknowledgedAsync(window.TenantId, window.SiteId, window.Id,
                    acknowledged.EnforcementRef, acknowledged.AcknowledgedAt, ct);
                break;
            case MaintenanceEnforcementResult.Conflict conflict:
                // Retrying cannot succeed while commitments overlap, so stop and report the window
                // as failed rather than claiming it is in force.
                logger.LogWarning(
                    "Maintenance window {WindowId} for site {SiteId} was refused by Operations with {CommitmentCount} overlapping commitment(s).",
                    window.Id, window.SiteId, conflict.Commitments.Count);
                await store.MarkFailedAsync(window.TenantId, window.SiteId, window.Id, ct);
                break;
        }
    }
}