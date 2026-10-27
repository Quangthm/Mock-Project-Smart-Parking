using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartParking.ParkingService.Application;
using SmartParking.ParkingService.Domain;

namespace SmartParking.ParkingService.Persistence;

public sealed class PostgresStructureSynchronizationStore(ParkingDbContext db, ParkingGateConnectionPool gates)
    : IStructureSynchronizationStore
{
    public async Task<IAsyncDisposable?> TryLockSiteAsync(Guid siteId, CancellationToken ct)
    {
        var lease = await gates.TryOpenAsync(ct);
        if (lease is null) return null;
        try
        {
            await using var command = new NpgsqlCommand(
                "SELECT pg_try_advisory_lock(hashtextextended($1::text,2))", lease.Connection);
            command.Parameters.AddWithValue(siteId);
            if ((bool)(await command.ExecuteScalarAsync(ct))!) return new SiteGate(lease, siteId);
            await lease.DisposeAsync();
            return null;
        }
        catch { await lease.DisposeAsync(); throw; }
    }

    private sealed class SiteGate(ParkingGateConnectionPool.ConnectionLease lease, Guid siteId) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var command = new NpgsqlCommand(
                    "SELECT pg_advisory_unlock(hashtextextended($1::text,2))", lease.Connection);
                command.Parameters.AddWithValue(siteId);
                await command.ExecuteNonQueryAsync();
            }
            finally { await lease.DisposeAsync(); }
        }
    }

    private static StructureSynchronization Map(StructureSynchronizationEntity e) =>
        new(e.Token, e.TenantId, e.SiteId, e.OperationId, e.State,
            e.Outcome is null ? null : JsonSerializer.Deserialize<StructureOutcome>(e.Outcome, StructureSynchronizationJson.Options));

    public async Task PrepareAsync(StructureSynchronization synchronization, CancellationToken ct)
    {
        var valid = await (from site in db.ParkingSites
            join tenant in db.Tenants on site.TenantId equals tenant.Id
            where site.Id == synchronization.SiteId && site.TenantId == synchronization.TenantId &&
                site.DeletedAt == null && tenant.Status == "ACTIVE" && tenant.DeletedAt == null
            select site.Id).AnyAsync(ct);
        if (!valid) throw new StructureException("SITE_NOT_FOUND", "Site is not in the active tenant scope.");
        db.StructureSynchronizations.Add(new StructureSynchronizationEntity
        {
            Token = synchronization.Token, TenantId = synchronization.TenantId, SiteId = synchronization.SiteId,
            OperationId = synchronization.OperationId, State = "preparing", Outcome = null,
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<StructureSynchronization>> PendingAsync(Guid? siteId, CancellationToken ct) =>
        (await db.StructureSynchronizations.AsNoTracking()
            .Where(x => x.State != "completed" && (siteId == null || x.SiteId == siteId))
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Token).ToListAsync(ct)).Select(Map).ToList();

    public async Task<StructureSynchronization> FencePreparationAsync(Guid token, CancellationToken ct)
    {
        var row = await db.StructureSynchronizations.FirstOrDefaultAsync(x => x.Token == token, ct)
            ?? throw new InvalidOperationException("Synchronization token does not exist.");
        if (row.State == "preparing")
        {
            row.State = "aborting";
            row.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        return Map(row);
    }

    public async Task AcknowledgeAsync(Guid token, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var sync = await db.StructureSynchronizations.FirstOrDefaultAsync(x => x.Token == token, ct);
        if (sync is null) return;
        if (sync.State == "ready" && sync.OperationId is { } operationId)
        {
            var operation = await db.StructureOperations.FirstOrDefaultAsync(x =>
                x.Id == operationId && x.PhysicalCommitted && x.SiteId == sync.SiteId && x.TenantId == sync.TenantId, ct);
            if (operation is not null) { operation.Status = "completed"; operation.Reason = null; operation.UpdatedAt = DateTimeOffset.UtcNow; }
        }
        sync.State = "completed";
        sync.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
