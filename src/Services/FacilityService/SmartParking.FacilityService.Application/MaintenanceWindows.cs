using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application;

public interface IMaintenanceWindowStore
{
    Task<IReadOnlyList<MaintenanceWindow>> ListAsync(OwnerScope owner, Guid siteId, CancellationToken ct);
    Task<MaintenanceWindow> CreateAsync(OwnerScope owner, Guid siteId, DateTime start, DateTime end, string reason, CancellationToken ct);
    Task<MaintenanceWindow> ExtendAsync(OwnerScope owner, Guid siteId, Guid windowId, DateTime? start, DateTime? end, CancellationToken ct);
    Task CancelAsync(OwnerScope owner, Guid siteId, Guid windowId, CancellationToken ct);
}

public sealed record ServiceState(string State, DateTime? ClosedUntil, IReadOnlyList<MaintenanceWindow> Windows);

// Windows are recorded and reported; they are never claimed to be in force. Enforcement stays
// 'pending' until an external acknowledgement exists (DE-13).
public sealed class MaintenanceWindowService(IMaintenanceWindowStore store, IOwnerAuthorizer owners)
{
    public async Task<IReadOnlyList<MaintenanceWindow>> ListAsync(OwnerScope owner, Guid siteId, CancellationToken ct)
    {
        await AuthorizeAsync(owner, ct);
        return await store.ListAsync(owner, siteId, ct);
    }

    public async Task<MaintenanceWindow> CreateAsync(
        OwnerScope owner, Guid siteId, DateTime start, DateTime end, string reason,
        Guid? unitId, Guid? slotId, CancellationToken ct)
    {
        await AuthorizeAsync(owner, ct);
        (start, end) = (Utc(start), Utc(end));
        MaintenanceWindowRules.ValidateCreate(unitId, slotId, reason, start, end, DateTime.UtcNow);
        return await store.CreateAsync(owner, siteId, start, end, reason.Trim(), ct);
    }

    public async Task<MaintenanceWindow> ExtendAsync(
        OwnerScope owner, Guid siteId, Guid windowId, DateTime? start, DateTime? end, CancellationToken ct)
    {
        await AuthorizeAsync(owner, ct);
        return await store.ExtendAsync(owner, siteId, windowId,
            start is { } from ? Utc(from) : null, end is { } to ? Utc(to) : null, ct);
    }

    public async Task CancelAsync(OwnerScope owner, Guid siteId, Guid windowId, CancellationToken ct)
    {
        await AuthorizeAsync(owner, ct);
        await store.CancelAsync(owner, siteId, windowId, ct);
    }

    public async Task<ServiceState> StateAsync(
        OwnerScope owner, Guid siteId, string siteStatus, CancellationToken ct)
    {
        await AuthorizeAsync(owner, ct);
        var windows = await store.ListAsync(owner, siteId, ct);
        var now = DateTime.UtcNow;
        return new(MaintenanceWindowState.Derive(windows, siteStatus, now),
            MaintenanceWindowState.ClosedUntil(windows, now), windows);
    }

    // PostgreSQL timestamptz requires an instant; a payload without an offset is read as UTC.
    private static DateTime Utc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private async Task AuthorizeAsync(OwnerScope owner, CancellationToken ct)
    {
        if (!await owners.IsOwnerAsync(owner, ct))
        {
            throw new StructureException("FORBIDDEN", "Current parking management access is required.");
        }
    }
}
