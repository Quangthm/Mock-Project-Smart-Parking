using Npgsql;

namespace SmartParking.ReservationService.Persistence;

public sealed record HoldSnapshot(Guid[] ProtectedSlots, bool HasLiveCommitments);
public sealed record PoolCapacity(Guid? UnitId, string VehicleType, int Capacity);
public sealed record EditOutcome(bool SiteActive, Guid[] RemovedSlots, Guid[] RemovedUnits, PoolCapacity[] Capacities);
public sealed class HoldConflictException : Exception;

// Owns only Reservation data. A durable hold has no unsafe automatic expiry.
public sealed class StructureSafetyStore(NpgsqlDataSource source)
{
    private static NpgsqlCommand Cmd(NpgsqlConnection db, NpgsqlTransaction tx, string sql, params object[] values)
    {
        var cmd = new NpgsqlCommand(sql, db, tx);
        foreach (var v in values) cmd.Parameters.Add(new NpgsqlParameter { Value = v });
        return cmd;
    }
    public async Task<HoldSnapshot> AcquireAsync(Guid tenantId, Guid siteId, Guid token, CancellationToken ct)
    {
        await using var db = await source.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        await using (var cmd = Cmd(db, tx, "SELECT pg_advisory_xact_lock(hashtextextended($1::text,0))", siteId)) await cmd.ExecuteNonQueryAsync(ct);
        await using (var cmd = Cmd(db, tx, "INSERT INTO structure_edit_holds(site_id,tenant_id,token) VALUES ($1,$2,$3) ON CONFLICT(site_id) DO NOTHING", siteId, tenantId, token)) await cmd.ExecuteNonQueryAsync(ct);
        await using (var cmd = Cmd(db, tx, "SELECT token FROM structure_edit_holds WHERE site_id=$1 AND tenant_id=$2", siteId, tenantId))
            if (await cmd.ExecuteScalarAsync(ct) is not Guid current || current != token) throw new HoldConflictException();
        var protectedIds = new List<Guid>();
        await using (var cmd = Cmd(db, tx, """
            SELECT slot_id FROM slot_allocations WHERE site_id=$1 AND allocation_status IN ('RESERVED','OCCUPIED')
            UNION SELECT current_slot_id FROM parking_sessions WHERE site_id=$1 AND exit_time IS NULL AND current_slot_id IS NOT NULL
            """, siteId))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct)) protectedIds.Add(reader.GetGuid(0));
        bool live;
        await using (var cmd = Cmd(db, tx, """
            SELECT EXISTS(SELECT 1 FROM reservations WHERE site_id=$1 AND status IN ('PENDING_PAYMENT','CONFIRMED','ALLOCATED','PARKING') AND expected_end_time>now())
            OR EXISTS(SELECT 1 FROM parking_sessions WHERE site_id=$1 AND exit_time IS NULL)
            OR EXISTS(SELECT 1 FROM slot_allocations WHERE site_id=$1 AND allocation_status IN ('RESERVED','OCCUPIED'))
            OR EXISTS(SELECT 1 FROM site_capacity_pools WHERE site_id=$1 AND (current_reserved_count>0 OR pending_payment_count>0 OR occupied_count>0 OR protected_count>0 OR backup_count>0))
            """, siteId)) live = (bool)(await cmd.ExecuteScalarAsync(ct))!;
        await tx.CommitAsync(ct);
        return new(protectedIds.ToArray(), live);
    }
    public async Task ReleaseAsync(Guid tenantId, Guid siteId, Guid token, EditOutcome? outcome, CancellationToken ct)
    {
        await using var db = await source.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        await using (var cmd = Cmd(db, tx, "SELECT pg_advisory_xact_lock(hashtextextended($1::text,0))", siteId)) await cmd.ExecuteNonQueryAsync(ct);
        await using (var cmd = Cmd(db, tx, "SELECT token FROM structure_edit_holds WHERE site_id=$1 AND tenant_id=$2", siteId, tenantId))
        {
            var current = await cmd.ExecuteScalarAsync(ct);
            if (current is null) { await tx.CommitAsync(ct); return; } // Retrying an acknowledged release.
            if (current is not Guid id || id != token) throw new HoldConflictException();
        }
        if (outcome is not null)
        {
            await using (var cmd = Cmd(db, tx, "INSERT INTO structure_site_state(site_id,tenant_id,active) VALUES ($1,$2,$3) ON CONFLICT(site_id) DO UPDATE SET active=EXCLUDED.active", siteId, tenantId, outcome.SiteActive)) await cmd.ExecuteNonQueryAsync(ct);
            foreach (var pair in new[] { (outcome.RemovedSlots, "slot"), (outcome.RemovedUnits, "unit") })
                foreach (var resource in pair.Item1)
                {
                    await using var cmd = Cmd(db, tx, "INSERT INTO structure_removed_resources(id,site_id,kind) VALUES ($1,$2,$3) ON CONFLICT DO NOTHING", resource, siteId, pair.Item2);
                    await cmd.ExecuteNonQueryAsync(ct);
                }
            // Remove the fence inside this transaction while retaining the advisory lock.
            // Other writers cannot observe the release until projections and capacities commit together.
            await using (var cmd = Cmd(db, tx, "DELETE FROM structure_edit_holds WHERE site_id=$1 AND token=$2", siteId, token)) await cmd.ExecuteNonQueryAsync(ct);
            await using (var cmd = Cmd(db, tx, """
                UPDATE site_capacity_pools p SET total_capacity=coalesce(c.capacity,0),
                    max_reservation_quota=least(p.max_reservation_quota,coalesce(c.capacity,0)),updated_at=now()
                FROM (SELECT p2.id, c.capacity FROM site_capacity_pools p2
                    LEFT JOIN jsonb_to_recordset($3::jsonb) AS c("unitId" uuid,"vehicleType" text,capacity int)
                    ON c."unitId" IS NOT DISTINCT FROM p2.spatial_unit_id AND c."vehicleType"=p2.vehicle_type
                    WHERE p2.site_id=$1 AND p2.tenant_id=$2) c
                WHERE p.id=c.id
                """, siteId, tenantId, System.Text.Json.JsonSerializer.Serialize(outcome.Capacities,
                    new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)))) await cmd.ExecuteNonQueryAsync(ct);
        }
        await using (var cmd = Cmd(db, tx, "DELETE FROM structure_edit_holds WHERE site_id=$1 AND token=$2", siteId, token)) await cmd.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
    }
}
