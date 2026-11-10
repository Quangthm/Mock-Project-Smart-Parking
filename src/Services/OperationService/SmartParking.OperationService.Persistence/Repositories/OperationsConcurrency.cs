using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;

namespace SmartParking.OperationService.Persistence.Repositories;

public sealed class OperationsConcurrency(ReservationDbContext db) : IOperationsConcurrency
{
    private readonly HashSet<(Guid Transaction, Guid Site)> sites = [];
    private readonly HashSet<(Guid Transaction, string Plate)> plates = [];
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
        var transaction = db.Database.CurrentTransaction ?? throw new InvalidOperationException("Vehicle guard requires a UoW transaction.");
        if (string.IsNullOrWhiteSpace(identity) || Domain.GateRules.NormalizePlate(identity) != identity)
            throw new InvalidOperationException("Vehicle guard requires the canonical plate.");
        var key = "vehicle-use:" + identity;
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
        plates.Add((transaction.TransactionId, identity));
    }
    public bool HoldsSite(Guid siteId) => db.Database.CurrentTransaction is { } transaction && sites.Contains((transaction.TransactionId, siteId));
    public bool HoldsPlate(string? plate) => plate is not null && db.Database.CurrentTransaction is { } transaction && plates.Contains((transaction.TransactionId, plate));
}
