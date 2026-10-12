using Npgsql;
using System.Text.Json;
using SmartParking.ParkingService.Application;

namespace SmartParking.ParkingService.Persistence;

public sealed class PostgresStructureSynchronizationStore(NpgsqlDataSource source, ParkingGateConnectionPool gates)
    : IStructureSynchronizationStore
{
    public async Task<IAsyncDisposable?> TryLockSiteAsync(Guid siteId, CancellationToken ct)
    {
        var lease = await gates.TryOpenAsync(ct);
        if (lease is null) return null;
        var connection = lease.Connection;
        try
        {
            await using var command = new NpgsqlCommand(
                "SELECT pg_try_advisory_lock(hashtextextended($1::text,2))", connection);
            command.Parameters.AddWithValue(siteId);
            if ((bool)(await command.ExecuteScalarAsync(ct))!)
                return new SiteGate(lease, siteId);
            await lease.DisposeAsync();
            return null;
        }
        catch
        {
            await lease.DisposeAsync();
            throw;
        }
    }

    private sealed class SiteGate(ParkingGateConnectionPool.ConnectionLease lease, Guid siteId) : IAsyncDisposable
    {
        private NpgsqlConnection Connection => lease.Connection;
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var command = new NpgsqlCommand(
                    "SELECT pg_advisory_unlock(hashtextextended($1::text,2))", Connection);
                command.Parameters.AddWithValue(siteId);
                await command.ExecuteNonQueryAsync();
            }
            finally { await lease.DisposeAsync(); }
        }
    }

    public async Task PrepareAsync(StructureSynchronization synchronization, CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        // Check scope locally before creating a hold in another service.
        await using var command = new NpgsqlCommand("""
            INSERT INTO structure_synchronizations(token,tenant_id,site_id,operation_id,state)
            SELECT $1,s.tenant_id,s.id,$4::uuid,'preparing' FROM parking_sites s
            JOIN tenants t ON t.id=s.tenant_id
            WHERE s.id=$3 AND s.tenant_id=$2 AND s.deleted_at IS NULL
                AND t.status='ACTIVE' AND t.deleted_at IS NULL
            """, connection);
        command.Parameters.AddWithValue(synchronization.Token);
        command.Parameters.AddWithValue(synchronization.TenantId);
        command.Parameters.AddWithValue(synchronization.SiteId);
        command.Parameters.Add(new NpgsqlParameter { Value = (object?)synchronization.OperationId ?? DBNull.Value });
        if (await command.ExecuteNonQueryAsync(ct) != 1)
            throw new SmartParking.ParkingService.Domain.StructureException(
                "SITE_NOT_FOUND", "Site is not in the active tenant scope.");
    }

    public async Task<IReadOnlyList<StructureSynchronization>> PendingAsync(Guid? siteId, CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("""
            SELECT token,tenant_id,site_id,operation_id,state,outcome::text
            FROM structure_synchronizations
            WHERE state<>'completed' AND ($1::uuid IS NULL OR site_id=$1)
            ORDER BY created_at,token
            """, connection);
        command.Parameters.Add(new NpgsqlParameter { Value = (object?)siteId ?? DBNull.Value });
        var result = new List<StructureSynchronization>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var outcome = reader.IsDBNull(5) ? null :
                JsonSerializer.Deserialize<StructureOutcome>(reader.GetString(5), StructureSynchronizationJson.Options);
            result.Add(new StructureSynchronization(
                reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2),
                reader.IsDBNull(3) ? null : reader.GetGuid(3), reader.GetString(4), outcome));
        }
        return result;
    }


    public async Task<StructureSynchronization> FencePreparationAsync(Guid token, CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        // This UPDATE waits for an in-flight physical transaction that has already
        // changed the same row to ready. If recovery wins first, CommitAsync can
        // no longer change preparing -> ready, so all physical writes roll back.
        await using (var fence = new NpgsqlCommand("""
            UPDATE structure_synchronizations SET state='aborting',updated_at=now()
            WHERE token=$1 AND state='preparing'
            """, connection))
        {
            fence.Parameters.AddWithValue(token);
            await fence.ExecuteNonQueryAsync(ct);
        }

        await using var command = new NpgsqlCommand("""
            SELECT token,tenant_id,site_id,operation_id,state,outcome::text
            FROM structure_synchronizations WHERE token=$1
            """, connection);
        command.Parameters.AddWithValue(token);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            throw new InvalidOperationException("Synchronization token does not exist.");

        var outcome = reader.IsDBNull(5) ? null :
            JsonSerializer.Deserialize<StructureOutcome>(reader.GetString(5), StructureSynchronizationJson.Options);
        return new StructureSynchronization(
            reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2),
            reader.IsDBNull(3) ? null : reader.GetGuid(3), reader.GetString(4), outcome);
    }

    public async Task AcknowledgeAsync(Guid token, CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        // A ready row proves the physical edit committed, so its operation may
        // finish. An abandoned preparation must not finish an unexecuted operation.
        await using (var operation = new NpgsqlCommand("""
            UPDATE structure_operations o SET status='completed',reason=NULL,updated_at=now()
            FROM structure_synchronizations s
            WHERE s.token=$1 AND s.state='ready' AND o.id=s.operation_id
                AND o.physical_committed AND o.site_id=s.site_id AND o.tenant_id=s.tenant_id
            """, connection, transaction))
        {
            operation.Parameters.AddWithValue(token);
            await operation.ExecuteNonQueryAsync(ct);
        }
        await using (var synchronization = new NpgsqlCommand(
            "UPDATE structure_synchronizations SET state='completed',updated_at=now() WHERE token=$1",
            connection, transaction))
        {
            synchronization.Parameters.AddWithValue(token);
            await synchronization.ExecuteNonQueryAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }
}
