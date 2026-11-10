using Npgsql;
using SmartParking.FacilityService.Application;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Persistence;

// Maintenance windows are recorded, never claimed effective: enforcement_state stays 'pending'
// until an external acknowledgement arrives (DE-13). The column is `service_window` because WINDOW
// is a reserved keyword in PostgreSQL; the wire keeps start/end.
public sealed class PostgresMaintenanceWindowStore(NpgsqlDataSource source) : IMaintenanceWindowStore
{
    private const string Columns = "id, tenant_id, site_id, unit_id, slot_id, lower(service_window), upper(service_window), " +
        "reason, created_by, created_at, cancelled_at, enforcement_state, enforcement_ref, acknowledged_at";

    private static NpgsqlCommand Command(
        NpgsqlConnection db, NpgsqlTransaction? tx, string sql, params object?[] values)
    {
        var command = new NpgsqlCommand(sql, db, tx);
        foreach (var value in values) command.Parameters.Add(new NpgsqlParameter { Value = value ?? DBNull.Value });
        return command;
    }
    private static MaintenanceWindow Window(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2),
        r.IsDBNull(3) ? null : r.GetGuid(3), r.IsDBNull(4) ? null : r.GetGuid(4), r.GetDateTime(5), r.GetDateTime(6),
        r.GetString(7), r.GetGuid(8), r.GetDateTime(9), r.IsDBNull(10) ? null : r.GetDateTime(10), r.GetString(11),
        r.IsDBNull(12) ? null : r.GetGuid(12), r.IsDBNull(13) ? null : r.GetDateTime(13));

    private static async Task EnsureSiteAsync(
        NpgsqlConnection db, NpgsqlTransaction? tx, OwnerScope owner, Guid siteId, CancellationToken ct)
    {
        await using var command = Command(db, tx,
            "SELECT 1 FROM parking_sites s WHERE s.id=$1 AND s.tenant_id=$2 AND s.deleted_at IS NULL", siteId, owner.TenantId);
        if (await command.ExecuteScalarAsync(ct) is null)
            throw new StructureException("SITE_NOT_FOUND", "Parking site was not found in this tenant.");
    }

    public async Task<IReadOnlyList<MaintenanceWindow>> ListAsync(OwnerScope owner, Guid siteId, CancellationToken ct)
    {
        await using var db = await source.OpenConnectionAsync(ct);
        await EnsureSiteAsync(db, null, owner, siteId, ct);
        var windows = new List<MaintenanceWindow>();
        await using var command = Command(db, null,
            $"SELECT {Columns} FROM structure_maintenance_windows WHERE tenant_id=$1 AND site_id=$2 AND cancelled_at IS NULL ORDER BY lower(service_window)",
            owner.TenantId, siteId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) windows.Add(Window(reader));
        return windows;
    }

    public async Task<MaintenanceWindow> CreateAsync(
        OwnerScope owner, Guid siteId, DateTime start, DateTime end, string reason, CancellationToken ct)
    {
        await using var db = await source.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        await EnsureSiteAsync(db, tx, owner, siteId, ct);
        try
        {
            MaintenanceWindow window;
            await using (var command = Command(db, tx, $"""
                INSERT INTO structure_maintenance_windows(id,tenant_id,site_id,unit_id,slot_id,service_window,reason,created_by,enforcement_state)
                SELECT $1,$3,$2,NULL,NULL,tstzrange($4::timestamptz,$5::timestamptz,'[)'),$6,$7,'pending'
                FROM parking_sites s WHERE s.id=$2 AND s.tenant_id=$3 AND s.deleted_at IS NULL
                RETURNING {Columns}
                """, Guid.NewGuid(), siteId, owner.TenantId, start, end, reason, owner.UserId))
            await using (var reader = await command.ExecuteReaderAsync(ct))
            {
                if (!await reader.ReadAsync(ct))
                    throw new StructureException("SITE_NOT_FOUND", "Parking site was not found in this tenant.");
                window = Window(reader);
            }
            await tx.CommitAsync(ct);
            return window;
        }
        catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.ExclusionViolation)
        {
            throw new StructureException("STRUCTURE_IN_USE", "Another maintenance window already covers this period.");
        }
    }

    public async Task<MaintenanceWindow?> GetByIdAsync(
        OwnerScope owner, Guid siteId, Guid windowId, CancellationToken ct)
    {
        await using var db = await source.OpenConnectionAsync(ct);
        await EnsureSiteAsync(db, null, owner, siteId, ct);
        await using var command = Command(db, null,
            $"SELECT {Columns} FROM structure_maintenance_windows WHERE id=$1 AND tenant_id=$2 AND site_id=$3 AND cancelled_at IS NULL",
            windowId, owner.TenantId, siteId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Window(reader) : null;
    }

    public async Task<MaintenanceWindow> ExtendAsync(
        OwnerScope owner, Guid siteId, Guid windowId, DateTime newEnd, CancellationToken ct)
    {
        await using var db = await source.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        await EnsureSiteAsync(db, tx, owner, siteId, ct);
        _ = await LockAsync(db, tx, owner, siteId, windowId, ct);
        try
        {
            MaintenanceWindow window;
            await using (var command = Command(db, tx, $"""
                UPDATE structure_maintenance_windows
                SET service_window = tstzrange(lower(service_window), $4::timestamptz, '[)'),
                    enforcement_state='pending', enforcement_ref=NULL, acknowledged_at=NULL
                WHERE id=$1 AND tenant_id=$2 AND site_id=$3 AND cancelled_at IS NULL
                RETURNING {Columns}
                """, windowId, owner.TenantId, siteId, newEnd))
            await using (var reader = await command.ExecuteReaderAsync(ct))
            {
                if (!await reader.ReadAsync(ct))
                    throw new StructureException("STRUCTURE_CONFLICT", "Maintenance window was not found in this site.");
                window = Window(reader);
            }
            await tx.CommitAsync(ct);
            return window;
        }
        catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.ExclusionViolation)
        {
            throw new StructureException("STRUCTURE_IN_USE", "Another maintenance window already covers this period.");
        }
    }

    public async Task CancelAsync(OwnerScope owner, Guid siteId, Guid windowId, CancellationToken ct)
    {
        await using var db = await source.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        await EnsureSiteAsync(db, tx, owner, siteId, ct);
        _ = await LockAsync(db, tx, owner, siteId, windowId, ct);
        // Reopening early must never bypass another closure that still covers now.
        await using (var other = Command(db, tx, """
            SELECT 1 FROM structure_maintenance_windows
            WHERE site_id=$1 AND tenant_id=$2 AND id<>$3 AND cancelled_at IS NULL AND service_window @> now()
            LIMIT 1
            """, siteId, owner.TenantId, windowId))
        {
            if (await other.ExecuteScalarAsync(ct) is not null)
                throw new StructureException("STRUCTURE_IN_USE", "Another maintenance window still covers this period.");
        }
        await using (var command = Command(db, tx,
            "UPDATE structure_maintenance_windows SET cancelled_at=now() WHERE id=$1 AND tenant_id=$2 AND site_id=$3 AND cancelled_at IS NULL",
            windowId, owner.TenantId, siteId))
            await command.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static async Task<MaintenanceWindow> LockAsync(
        NpgsqlConnection db, NpgsqlTransaction tx, OwnerScope owner, Guid siteId, Guid windowId, CancellationToken ct)
    {
        await using var command = Command(db, tx,
            $"SELECT {Columns} FROM structure_maintenance_windows WHERE id=$1 AND tenant_id=$2 AND site_id=$3 AND cancelled_at IS NULL FOR UPDATE",
            windowId, owner.TenantId, siteId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            throw new StructureException("STRUCTURE_CONFLICT", "Maintenance window was not found in this site.");
        return Window(reader);
    }
}
