using System.Text.Json;

namespace SmartParking.FacilityService.Application;

public static class StructureSynchronizationJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}

// Both the foreground request and the worker call this use case under the same
// site gate. A preparing row means there was no committed physical change:
// the transition to ready was atomic with the Parking transaction.
public sealed class StructureSynchronizationService(
    IStructureSynchronizationStore store, IStructureCommitments commitments)
{
    public async Task RecoverSiteAsync(Guid siteId, CancellationToken cancellationToken)
    {
        var pending = await store.PendingAsync(siteId, cancellationToken);
        foreach (var synchronization in pending)
        {
            await PublishAsync(synchronization, cancellationToken);
        }
    }

    public async Task PublishAsync(
        StructureSynchronization synchronization, CancellationToken cancellationToken)
    {
        if (synchronization.State == "preparing")
            synchronization = await store.FencePreparationAsync(synchronization.Token, cancellationToken);
        if (synchronization.State == "completed") return;
        if (synchronization.State == "ready" && synchronization.Outcome is null)
            throw new InvalidOperationException("A committed edit must have a replay outcome.");

        await commitments.ReleaseAsync(
            synchronization.TenantId, synchronization.SiteId, synchronization.Token,
            synchronization.State == "ready" ? synchronization.Outcome : null,
            cancellationToken);
        await store.AcknowledgeAsync(synchronization.Token, cancellationToken);
    }

    public async Task<IReadOnlyList<(Guid SiteId, Exception Error)>> RecoverPendingAsync(CancellationToken cancellationToken)
    {
        var failures = new List<(Guid SiteId, Exception Error)>();
        var pending = await store.PendingAsync(null, cancellationToken);
        foreach (var siteId in pending.Select(row => row.SiteId).Distinct())
        {
            try
            {
                await using var gate = await store.TryLockSiteAsync(siteId, cancellationToken);
                if (gate is null) continue;

                // Re-read after locking; the request may have acknowledged rows
                // between our first query and the acquisition of this gate.
                await RecoverSiteAsync(siteId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception error)
            {
                // One unavailable site must not prevent other sites recovering.
                failures.Add((siteId, error));
            }
        }
        return failures;
    }
}
