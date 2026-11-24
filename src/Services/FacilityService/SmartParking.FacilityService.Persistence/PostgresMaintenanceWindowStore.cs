using Npgsql;
using SmartParking.Contracts.Facility;
using SmartParking.FacilityService.Application;
using SmartParking.FacilityService.Application.Common.Interfaces.Services;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Persistence;

// Maintenance windows are recorded, never claimed effective: enforcement_state stays 'pending'
// until an external acknowledgement arrives (DE-13). The column is `service_window` because WINDOW
// is a reserved keyword in PostgreSQL; the wire keeps start/end.
public sealed class PostgresMaintenanceWindowStore(
    NpgsqlDataSource source,
    ParkingDbContext? context = null,
    ILayoutInvalidationPublisher? layoutInvalidations = null) : IMaintenanceWindowStore
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
        OwnerScope owner, Guid siteId, DateTime start, DateTime end, string reason, CancellationToken ct,
        Guid? unitId = null, Guid? slotId = null)
    {
        await using var txScope = await FacilityOutboxTransaction.BeginAsync(source, context, ct);
        var db = txScope.Connection;
        var tx = txScope.Transaction;
        await EnsureSiteAsync(db, tx, owner, siteId, ct);
        // DE-3: a window may cover the whole site, one unit subtree, or a single slot.
        if (unitId is { } targetUnit)
        {
            await using var unit = Command(db, tx,
                "SELECT 1 FROM spatial_units u WHERE u.id=$1 AND u.site_id=$2 AND u.tenant_id=$3 AND u.deleted_at IS NULL",
                targetUnit, siteId, owner.TenantId);
            if (await unit.ExecuteScalarAsync(ct) is null)
                throw new StructureException("UNIT_NOT_FOUND", "Spatial unit does not belong to this site.");
        }
        if (slotId is { } targetSlot)
        {
            await using var slot = Command(db, tx,
                "SELECT 1 FROM parking_slots s WHERE s.id=$1 AND s.spatial_unit_id=$2 AND s.site_id=$3 AND s.tenant_id=$4 AND s.deleted_at IS NULL",
                targetSlot, unitId, siteId, owner.TenantId);
            if (await slot.ExecuteScalarAsync(ct) is null)
                throw new StructureException("SLOT_NOT_FOUND", "Slot does not belong to the specified unit in this site.");
        }
        // A site-wide window may not overlap any live window; a scoped window may not overlap a
        // live site-wide one. Same-unit and same-slot overlaps are rejected by exclusion constraints.
        await using (var overlap = Command(db, tx, unitId is null && slotId is null
            ? """
              SELECT 1 FROM structure_maintenance_windows
              WHERE site_id=$1 AND tenant_id=$2 AND cancelled_at IS NULL
                AND service_window && tstzrange($3::timestamptz,$4::timestamptz,'[)')
              LIMIT 1
              """
            : """
              SELECT 1 FROM structure_maintenance_windows
              WHERE site_id=$1 AND tenant_id=$2 AND cancelled_at IS NULL
                AND unit_id IS NULL AND slot_id IS NULL
                AND service_window && tstzrange($3::timestamptz,$4::timestamptz,'[)')
              LIMIT 1
              """, siteId, owner.TenantId, start, end))
        {
            if (await overlap.ExecuteScalarAsync(ct) is not null)
                throw new StructureException("STRUCTURE_IN_USE", "Another maintenance window already covers this period.");
        }
        try
        {
            MaintenanceWindow window;
            await using (var command = Command(db, tx, $"""
                INSERT INTO structure_maintenance_windows(id,tenant_id,site_id,unit_id,slot_id,service_window,reason,created_by,enforcement_state)
                SELECT $1,$3,$2,$8,$9,tstzrange($4::timestamptz,$5::timestamptz,'[)'),$6,$7,'pending'
                FROM parking_sites s WHERE s.id=$2 AND s.tenant_id=$3 AND s.deleted_at IS NULL
                RETURNING {Columns}
                """, Guid.NewGuid(), siteId, owner.TenantId, start, end, reason, owner.UserId, unitId, slotId))
            await using (var reader = await command.ExecuteReaderAsync(ct))
            {
                if (!await reader.ReadAsync(ct))
                    throw new StructureException("SITE_NOT_FOUND", "Parking site was not found in this tenant.");
                window = Window(reader);
            }
            await txScope.StageAndCommitAsync(
                layoutInvalidations,
                owner.TenantId,
                siteId,
                [LayoutInvalidationResources.Structure],
                ct);
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
        await using var txScope = await FacilityOutboxTransaction.BeginAsync(source, context, ct);
        var db = txScope.Connection;
        var tx = txScope.Transaction;
        await EnsureSiteAsync(db, tx, owner, siteId, ct);
        var current = await LockAsync(db, tx, owner, siteId, windowId, ct);
        // Extension can widen a closure into a period another live window already owns.
        // Mirrors the create-time cross-scope guard so the caller gets STRUCTURE_IN_USE
        // instead of relying solely on the database exclusion constraints.
        var siteWide = current.UnitId is null && current.SlotId is null;
        await using (var overlap = Command(db, tx, siteWide
            ? """
              SELECT 1 FROM structure_maintenance_windows
              WHERE site_id=$1 AND tenant_id=$2 AND id<>$3 AND cancelled_at IS NULL
                AND service_window && tstzrange($4::timestamptz, $5::timestamptz, '[)')
              LIMIT 1
              """
            : """
              SELECT 1 FROM structure_maintenance_windows
              WHERE site_id=$1 AND tenant_id=$2 AND id<>$3 AND cancelled_at IS NULL
                AND unit_id IS NULL AND slot_id IS NULL
                AND service_window && tstzrange($4::timestamptz, $5::timestamptz, '[)')
              LIMIT 1
              """, siteId, owner.TenantId, windowId, current.Start, newEnd))
        {
            if (await overlap.ExecuteScalarAsync(ct) is not null)
                throw new StructureException("STRUCTURE_IN_USE", "Another maintenance window already covers this period.");
        }
        try
        {
            MaintenanceWindow window;
            await using (var command = Command(db, tx, $"""
                UPDATE structure_maintenance_windows
                SET service_window = tstzrange(lower(service_window), $4::timestamptz, '[)'),
                    enforcement_state='pending', enforcement_ref=NULL, acknowledged_at=NULL,
                    end_notified_at=NULL
                WHERE id=$1 AND tenant_id=$2 AND site_id=$3 AND cancelled_at IS NULL
                RETURNING {Columns}
                """, windowId, owner.TenantId, siteId, newEnd))
            await using (var reader = await command.ExecuteReaderAsync(ct))
            {
                if (!await reader.ReadAsync(ct))
                    throw new StructureException("STRUCTURE_CONFLICT", "Maintenance window was not found in this site.");
                window = Window(reader);
            }
            if (window.End != current.End ||
                !string.Equals(window.EnforcementState, current.EnforcementState, StringComparison.Ordinal))
            {
                await txScope.StageAndCommitAsync(
                    layoutInvalidations,
                    owner.TenantId,
                    siteId,
                    [LayoutInvalidationResources.Structure],
                    ct);
            }
            else
            {
                await txScope.CommitWithoutStagingAsync(ct);
            }
            return window;
        }
        catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.ExclusionViolation)
        {
            throw new StructureException("STRUCTURE_IN_USE", "Another maintenance window already covers this period.");
        }
    }

    public async Task CancelAsync(OwnerScope owner, Guid siteId, Guid windowId, CancellationToken ct)
    {
        await using var txScope = await FacilityOutboxTransaction.BeginAsync(source, context, ct);
        var db = txScope.Connection;
        var tx = txScope.Transaction;
        await EnsureSiteAsync(db, tx, owner, siteId, ct);
        var current = await LockAsync(db, tx, owner, siteId, windowId, ct);
        // Reopening early must never bypass another closure that still covers now AND
        // overlaps this window's own scope; unrelated windows must not block the cancel.
        if (MaintenanceWindowState.Covers(current, DateTime.UtcNow))
        {
            await using var other = Command(db, tx, """
                SELECT 1 FROM structure_maintenance_windows
                WHERE site_id=$1 AND tenant_id=$2 AND id<>$3 AND cancelled_at IS NULL
                  AND service_window @> now()
                  AND (
                      (unit_id IS NULL AND slot_id IS NULL)
                      OR ($5::uuid IS NULL AND $4::uuid IS NOT NULL AND unit_id = $4)
                      OR ($5::uuid IS NOT NULL AND $4::uuid IS NOT NULL AND unit_id = $4 AND slot_id IS NULL)
                      OR ($5::uuid IS NOT NULL AND slot_id = $5)
                  )
                LIMIT 1
                """, siteId, owner.TenantId, windowId, current.UnitId, current.SlotId);
            if (await other.ExecuteScalarAsync(ct) is not null)
                throw new StructureException("STRUCTURE_IN_USE", "Another maintenance window still covers this period.");
        }
        int affected;
        await using (var command = Command(db, tx,
            "UPDATE structure_maintenance_windows SET cancelled_at=now() WHERE id=$1 AND tenant_id=$2 AND site_id=$3 AND cancelled_at IS NULL",
            windowId, owner.TenantId, siteId))
            affected = await command.ExecuteNonQueryAsync(ct);

        if (affected > 0)
        {
            // An acknowledged window is live in the operator projection, so cancelling it must
            // publish the full invalidation set to drop the MAINTENANCE overlay immediately.
            IReadOnlyList<string> resources = string.Equals(current.EnforcementState, "acknowledged", StringComparison.Ordinal)
                ? [LayoutInvalidationResources.Structure, LayoutInvalidationResources.FullLayout, LayoutInvalidationResources.CapacitySummary]
                : [LayoutInvalidationResources.Structure];
            await txScope.StageAndCommitAsync(
                layoutInvalidations,
                owner.TenantId,
                siteId,
                resources,
                ct);
        }
        else
        {
            await txScope.CommitWithoutStagingAsync(ct);
        }
    }

    public async Task<MaintenanceWindowScope> ResolveScopeAsync(
        Guid tenantId, Guid siteId, Guid? unitId, Guid? slotId, CancellationToken ct)
    {
        if (unitId is not { } targetUnit) return new MaintenanceWindowScope([], []);
        // A slot-scoped window covers only itself; a unit-scoped window covers the whole subtree.
        if (slotId is { } targetSlot) return new MaintenanceWindowScope([targetUnit], [targetSlot]);

        await using var db = await source.OpenConnectionAsync(ct);
        var units = new List<Guid>();
        var slots = new List<Guid>();
        await using (var unitCommand = Command(db, null, """
            SELECT subtree.id
            FROM spatial_units target
            JOIN spatial_units subtree
              ON subtree.site_id = target.site_id AND subtree.tenant_id = target.tenant_id
             AND subtree.deleted_at IS NULL AND subtree.path LIKE target.path || '%'
            WHERE target.id=$1 AND target.site_id=$2 AND target.tenant_id=$3 AND target.deleted_at IS NULL
            """, targetUnit, siteId, tenantId))
        await using (var reader = await unitCommand.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct)) units.Add(reader.GetGuid(0));
        }

        await using (var slotCommand = Command(db, null, """
            SELECT slot.id
            FROM spatial_units target
            JOIN spatial_units subtree
              ON subtree.site_id = target.site_id AND subtree.tenant_id = target.tenant_id
             AND subtree.deleted_at IS NULL AND subtree.path LIKE target.path || '%'
            JOIN parking_slots slot
              ON slot.spatial_unit_id = subtree.id AND slot.deleted_at IS NULL
            WHERE target.id=$1 AND target.site_id=$2 AND target.tenant_id=$3 AND target.deleted_at IS NULL
            ORDER BY slot.slot_code
            """, targetUnit, siteId, tenantId))
        await using (var reader = await slotCommand.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct)) slots.Add(reader.GetGuid(0));
        }

        return new MaintenanceWindowScope(units, slots);
    }

    public async Task<MaintenanceWindow> MarkAcknowledgedAsync(
        Guid tenantId, Guid siteId, Guid windowId, Guid enforcementRef, DateTime acknowledgedAt, CancellationToken ct)
    {
        await using var txScope = await FacilityOutboxTransaction.BeginAsync(source, context, ct);
        var db = txScope.Connection;
        var tx = txScope.Transaction;
        MaintenanceWindow window;
        await using (var command = Command(db, tx, $"""
            UPDATE structure_maintenance_windows
            SET enforcement_state='acknowledged', enforcement_ref=$4, acknowledged_at=$5
            WHERE id=$1 AND tenant_id=$2 AND site_id=$3 AND cancelled_at IS NULL
            RETURNING {Columns}
            """, windowId, tenantId, siteId, enforcementRef, acknowledgedAt))
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct))
                throw new StructureException("STRUCTURE_CONFLICT", "Maintenance window was not found in this site.");
            window = Window(reader);
        }
        await txScope.StageAndCommitAsync(
            layoutInvalidations,
            tenantId,
            siteId,
            [
                LayoutInvalidationResources.Structure,
                LayoutInvalidationResources.FullLayout,
                LayoutInvalidationResources.CapacitySummary
            ],
            ct);
        return window;
    }

    public async Task MarkFailedAsync(Guid tenantId, Guid siteId, Guid windowId, CancellationToken ct)
    {
        await using var db = await source.OpenConnectionAsync(ct);
        await using var command = Command(db, null,
            "UPDATE structure_maintenance_windows SET enforcement_state='failed' WHERE id=$1 AND tenant_id=$2 AND site_id=$3 AND cancelled_at IS NULL",
            windowId, tenantId, siteId);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task DeletePendingAsync(Guid tenantId, Guid siteId, Guid windowId, CancellationToken ct)
    {
        await using var txScope = await FacilityOutboxTransaction.BeginAsync(source, context, ct);
        var db = txScope.Connection;
        var tx = txScope.Transaction;
        int affected;
        await using (var command = Command(db, tx, """
            UPDATE structure_maintenance_windows
            SET cancelled_at=now()
            WHERE id=$1 AND tenant_id=$2 AND site_id=$3 AND enforcement_state='pending' AND cancelled_at IS NULL
            """, windowId, tenantId, siteId))
        {
            affected = await command.ExecuteNonQueryAsync(ct);
        }
        if (affected > 0)
        {
            await txScope.StageAndCommitAsync(
                layoutInvalidations, tenantId, siteId, [LayoutInvalidationResources.Structure], ct);
        }
        else
        {
            await txScope.CommitWithoutStagingAsync(ct);
        }
    }

    public async Task<MaintenanceWindow> RevertExtensionAsync(
        Guid tenantId, Guid siteId, Guid windowId, DateTime previousEnd, string previousEnforcementState,
        Guid? previousEnforcementRef, DateTime? previousAcknowledgedAt, CancellationToken ct)
    {
        await using var txScope = await FacilityOutboxTransaction.BeginAsync(source, context, ct);
        var db = txScope.Connection;
        var tx = txScope.Transaction;
        MaintenanceWindow window;
        await using (var command = Command(db, tx, $"""
            UPDATE structure_maintenance_windows
            SET service_window = tstzrange(lower(service_window), $4::timestamptz, '[)'),
                enforcement_state=$5, enforcement_ref=$6, acknowledged_at=$7
            WHERE id=$1 AND tenant_id=$2 AND site_id=$3 AND cancelled_at IS NULL
            RETURNING {Columns}
            """, windowId, tenantId, siteId, previousEnd, previousEnforcementState, previousEnforcementRef,
            previousAcknowledgedAt))
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct))
                throw new StructureException("STRUCTURE_CONFLICT", "Maintenance window was not found in this site.");
            window = Window(reader);
        }
        await txScope.StageAndCommitAsync(
            layoutInvalidations, tenantId, siteId, [LayoutInvalidationResources.Structure], ct);
        return window;
    }

    public async Task ClearEnforcementRefAsync(Guid tenantId, Guid siteId, Guid windowId, CancellationToken ct)
    {
        await using var db = await source.OpenConnectionAsync(ct);
        await using var command = Command(db, null,
            "UPDATE structure_maintenance_windows SET enforcement_ref=NULL WHERE id=$1 AND tenant_id=$2 AND site_id=$3 AND cancelled_at IS NOT NULL",
            windowId, tenantId, siteId);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<MaintenanceWindow>> ListPendingEnforcementAsync(DateTime now, CancellationToken ct)
    {
        await using var db = await source.OpenConnectionAsync(ct);
        var windows = new List<MaintenanceWindow>();
        await using var command = Command(db, null,
            $"SELECT {Columns} FROM structure_maintenance_windows WHERE cancelled_at IS NULL AND enforcement_state='pending' AND upper(service_window) > $1 ORDER BY created_at",
            now);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) windows.Add(Window(reader));
        return windows;
    }

    public async Task<IReadOnlyList<MaintenanceWindow>> ListCancelledPendingRemoteSyncAsync(CancellationToken ct)
    {
        await using var db = await source.OpenConnectionAsync(ct);
        var windows = new List<MaintenanceWindow>();
        await using var command = Command(db, null,
            $"SELECT {Columns} FROM structure_maintenance_windows WHERE cancelled_at IS NOT NULL AND enforcement_ref IS NOT NULL ORDER BY cancelled_at");
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) windows.Add(Window(reader));
        return windows;
    }

    // Watermarks make each boundary transition observable exactly once, however often the ticker runs.
    public async Task<int> NotifyDueBoundariesAsync(DateTime now, CancellationToken ct)
    {
        await using var txScope = await FacilityOutboxTransaction.BeginAsync(source, context, ct);
        var db = txScope.Connection;
        var tx = txScope.Transaction;
        var affected = new HashSet<(Guid TenantId, Guid SiteId)>();
        var total = 0;

        async Task<int> MarkAsync(string sql)
        {
            var count = 0;
            await using var command = Command(db, tx, sql, now);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                affected.Add((reader.GetGuid(0), reader.GetGuid(1)));
                count++;
            }
            return count;
        }

        total += await MarkAsync("""
            UPDATE structure_maintenance_windows
            SET start_notified_at = $1
            WHERE cancelled_at IS NULL AND enforcement_state='acknowledged'
              AND lower(service_window) <= $1 AND upper(service_window) > $1 AND start_notified_at IS NULL
            RETURNING tenant_id, site_id
            """);
        total += await MarkAsync("""
            UPDATE structure_maintenance_windows
            SET end_notified_at = $1
            WHERE cancelled_at IS NULL AND enforcement_state='acknowledged'
              AND upper(service_window) <= $1 AND end_notified_at IS NULL
            RETURNING tenant_id, site_id
            """);

        foreach (var (tenantId, siteId) in affected)
        {
            await txScope.StageAsync(layoutInvalidations, tenantId, siteId,
                [
                    LayoutInvalidationResources.Structure,
                    LayoutInvalidationResources.FullLayout,
                    LayoutInvalidationResources.CapacitySummary
                ],
                ct);
        }

        await txScope.CommitWithoutStagingAsync(ct);
        return total;
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
