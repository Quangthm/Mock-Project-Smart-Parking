using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartParking.FacilityService.Application;
using SmartParking.FacilityService.Domain;
namespace SmartParking.FacilityService.Persistence;

public sealed class StructureOperationStore(ParkingDbContext db, ParkingGateConnectionPool gates) : IStructureOperationStore
{

    public async Task<IAsyncDisposable> LockAsync(Guid id, CancellationToken ct)
    {
        var lease = await gates.TryOpenAsync(ct)
            ?? throw new StructureException("STRUCTURE_BUSY", "Session gate capacity is busy; retry later.");
        try
        {
            await using var command = Cmd(lease.Connection,
                "SELECT pg_try_advisory_lock(hashtextextended($1::text,1))", id);
            if (!(bool)(await command.ExecuteScalarAsync(ct))!)
                throw new StructureException("STRUCTURE_BUSY", "Another request is processing this operation.");
            return new Gate(lease, id);
        }
        catch
        {
            await lease.DisposeAsync();
            throw;
        }
    }

    private sealed class Gate(ParkingGateConnectionPool.ConnectionLease lease, Guid id) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var command = Cmd(lease.Connection,
                    "SELECT pg_advisory_unlock(hashtextextended($1::text,1))", id);
                await command.ExecuteNonQueryAsync();
            }
            finally { await lease.DisposeAsync(); }
        }
    }
    private static NpgsqlCommand Cmd(NpgsqlConnection db, string sql, params object?[] values)
    {
        var command = new NpgsqlCommand(sql, db);
        foreach (var value in values) command.Parameters.Add(new NpgsqlParameter { Value = value ?? DBNull.Value });
        return command;
    }
    public async Task<StructureOperation> BeginAsync(OwnerScope owner, Guid site, Guid key, StructureEdit edit, CancellationToken ct)
    {
        var request = JsonSerializer.Serialize(edit);
        var siteExists = await db.ParkingSites.AnyAsync(x => x.Id == site && x.TenantId == owner.TenantId && x.DeletedAt == null, ct);
        if (!siteExists) throw new StructureException("FORBIDDEN", "Operation scope is not available.");
        var operation = await db.StructureOperations.FirstOrDefaultAsync(x =>
            x.SiteId == site && x.IdempotencyKey == key && x.TenantId == owner.TenantId && x.ActorId == owner.UserId, ct);
        if (operation is null)
        {
            operation = new StructureOperationEntity { Id = Guid.NewGuid(), SiteId = site, TenantId = owner.TenantId,
                ActorId = owner.UserId, IdempotencyKey = key, Request = request, Status = "pending", UpdatedAt = DateTimeOffset.UtcNow };
            db.StructureOperations.Add(operation);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException) { db.Entry(operation).State = EntityState.Detached; operation = await db.StructureOperations.SingleAsync(x => x.SiteId == site && x.IdempotencyKey == key, ct); }
        }
        if (operation.Request != request) throw new StructureException("DUPLICATE_IDENTIFIER", "Idempotency key belongs to a different request.");
        return new(operation.Id, site, operation.Status, operation.Reason, operation.Impact);
    }
    public async Task<StructureOperation> UpdateAsync(OwnerScope owner, Guid id, string status, string? reason, string? impact, CancellationToken ct)
    {
        var operation = await db.StructureOperations.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == owner.TenantId && x.ActorId == owner.UserId, ct);
        if (operation is not null && operation.Status != "completed")
        {
            operation.Status = status; operation.Reason = reason; operation.Impact = impact; operation.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return new(id, operation.SiteId, operation.Status, operation.Reason, operation.Impact);
        }
        if (operation is not null && operation.Status == "completed")
            return new(id, operation.SiteId, operation.Status, operation.Reason, operation.Impact);
        throw new StructureException("STRUCTURE_CONFLICT", "Operation is not in scope.");
    }
    public async Task<StructureOperation> ReadAsync(OwnerScope owner, Guid site, Guid id, CancellationToken ct)
    {
        var operation = await db.StructureOperations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.SiteId == site && x.TenantId == owner.TenantId && x.ActorId == owner.UserId, ct);
        if (operation is null) throw new StructureException("SITE_NOT_FOUND", "Operation was not found.");
        return new(id, site, operation.Status, operation.Reason, operation.Impact);
    }
}
