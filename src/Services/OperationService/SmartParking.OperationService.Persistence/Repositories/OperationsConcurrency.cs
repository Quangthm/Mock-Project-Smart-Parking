using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;

namespace SmartParking.OperationService.Persistence.Repositories;

public sealed class OperationsConcurrency(ReservationDbContext db) : IOperationsConcurrency
{
    private readonly HashSet<(Guid Transaction, Guid Site)> sites = [];
    public async Task LockSiteAsync(Guid siteId, CancellationToken ct)
    {
        var transaction = db.Database.CurrentTransaction ?? throw new InvalidOperationException("Site guard requires a UoW transaction.");
        if (sites.Contains((transaction.TransactionId, siteId))) return;
        // Same key as the removed trigger; acquire BEFORE reads, in ReadCommitted isolation.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({siteId.ToString()}, 0))", ct);
        sites.Add((transaction.TransactionId, siteId));
    }
    public async Task LockVehicleAsync(string identity, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Vehicle guard requires a UoW transaction.");
        var key = "vehicle-use:" + identity;
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
    }
    public bool HoldsSite(Guid siteId) => db.Database.CurrentTransaction is { } transaction && sites.Contains((transaction.TransactionId, siteId));
}
