using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using SmartParking.Contracts.Facility;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Common.Interfaces.Services;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Persistence;

// Immediate physical closure storage. Every mutation writes the restriction row, the parking-slot
// state, the audit row and both integration events inside ONE FacilityOutboxTransaction so a
// message can never be published for state that was rolled back.
public sealed class PostgresEmergencyRestrictionStore(
    NpgsqlDataSource source,
    ParkingDbContext? context = null,
    ILayoutInvalidationPublisher? layoutInvalidations = null,
    IFacilityCapacityRestrictionPublisher? restrictionsPublisher = null,
    TimeProvider? clock = null) : IEmergencyRestrictionStore
{
    private const string Columns =
        "id, tenant_id, site_id, mode, scope, unit_id, slot_id, affected_slot_ids, reason, " +
        "effective_from, effective_until, created_by, created_at, released_at, released_by";

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<IReadOnlyList<EmergencyRestriction>> ListActiveAsync(
        Guid tenantId,
        Guid siteId,
        CancellationToken ct)
    {
        await using var db = await source.OpenConnectionAsync(ct);
        await using var cmd = Command(db, null, $"""
            SELECT {Columns}
            FROM structure_emergency_restrictions
            WHERE tenant_id=$1 AND site_id=$2 AND released_at IS NULL
              AND (effective_until IS NULL OR effective_until > now())
            ORDER BY created_at DESC
            """, tenantId, siteId);

        var results = new List<EmergencyRestriction>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) results.Add(Map(r));
        return results;
    }

    public async Task<EmergencyRestriction> CreateAsync(
        Guid tenantId,
        Guid siteId,
        Guid actorId,
        string actorRole,
        string mode,
        string scope,
        Guid? unitId,
        Guid? slotId,
        string reason,
        DateTimeOffset? effectiveUntil,
        CancellationToken ct)
    {
        await using var txScope = await FacilityOutboxTransaction.BeginAsync(source, context, ct);
        var db = txScope.Connection;
        var tx = txScope.Transaction;

        await EnsureSiteAsync(db, tx, tenantId, siteId, ct);
        if (unitId is { } targetUnit) await EnsureUnitAsync(db, tx, tenantId, siteId, targetUnit, ct);
        // A slot restriction persists the slot's leaf unit, not the Floor/Zone ancestor the operator
        // picked, so the stored unit_id always points at the bay itself.
        var persistedUnitId = unitId;
        if (slotId is { } targetSlot)
            persistedUnitId = await EnsureSlotAsync(db, tx, tenantId, siteId, unitId!.Value, targetSlot, ct);

        var affectedSlotIds = await ResolveAffectedSlotsAsync(db, tx, tenantId, siteId, scope, unitId, slotId, ct);
        var effectiveFrom = _clock.GetUtcNow();

        // NEVER displaces a parked vehicle: occupied bays keep their state but stay in scope so
        // admission and future reservations covering them are still blocked.
        if (affectedSlotIds.Count > 0)
        {
            var state = mode == EmergencyRestrictionRules.ForcedMaintenanceMode ? "MAINTENANCE" : "UNAVAILABLE";
            await using var update = Command(db, tx, """
                UPDATE parking_slots
                SET physical_state = $1, updated_at = now()
                WHERE tenant_id = $2 AND site_id = $3 AND id = ANY($4::uuid[])
                  AND deleted_at IS NULL AND physical_state <> 'OCCUPIED'
                """, state, tenantId, siteId, AddUuids(affectedSlotIds));
            await update.ExecuteNonQueryAsync(ct);
        }

        EmergencyRestriction created;
        await using (var insert = Command(db, tx, $"""
            INSERT INTO structure_emergency_restrictions
                (id, tenant_id, site_id, mode, scope, unit_id, slot_id, affected_slot_ids,
                 reason, effective_from, effective_until, created_by)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8::jsonb, $9, $10, $11, $12)
            RETURNING {Columns}
            """,
            Guid.NewGuid(), tenantId, siteId, mode, scope, persistedUnitId, slotId,
            JsonSerializer.Serialize(affectedSlotIds), reason, effectiveFrom, effectiveUntil, actorId))
        await using (var r = await insert.ExecuteReaderAsync(ct))
        {
            if (!await r.ReadAsync(ct))
                throw new StructureException("STRUCTURE_CONFLICT", "The restriction could not be recorded.");
            created = Map(r);
        }

        await AuditAsync(db, tx, tenantId, siteId, actorId, actorRole,
            "EMERGENCY_RESTRICTION_CREATED", scope, created.Id, reason, ct);

        var eventId = Guid.NewGuid();
        var occurredAt = _clock.GetUtcNow();
        await txScope.StageActionAsync(async token =>
        {
            if (layoutInvalidations is not null)
                await layoutInvalidations.StageAsync(tenantId, siteId,
                    [
                        LayoutInvalidationResources.Structure,
                        LayoutInvalidationResources.FullLayout,
                        LayoutInvalidationResources.CapacitySummary
                    ], token);

            if (restrictionsPublisher is not null)
                await restrictionsPublisher.StageRestrictedAsync(
                    new FacilityCapacityRestrictedV1(
                        eventId,
                        FacilityCapacityRestrictionTopology.CurrentSchemaVersion,
                        occurredAt,
                        tenantId,
                        siteId,
                        created.Id,
                        created.Mode,
                        created.Scope,
                        created.UnitId,
                        created.AffectedSlotIds,
                        created.EffectiveFrom,
                        created.EffectiveUntil,
                        created.Reason),
                    token);
        }, ct);

        await txScope.CommitWithoutStagingAsync(ct);
        return created;
    }

    public async Task<EmergencyRestriction?> ReleaseAsync(
        Guid tenantId,
        Guid siteId,
        Guid restrictionId,
        Guid actorId,
        string actorRole,
        CancellationToken ct)
    {
        await using var txScope = await FacilityOutboxTransaction.BeginAsync(source, context, ct);
        var db = txScope.Connection;
        var tx = txScope.Transaction;

        EmergencyRestriction released;
        await using (var release = Command(db, tx, $"""
            UPDATE structure_emergency_restrictions
            SET released_at = now(), released_by = $4
            WHERE id = $1 AND tenant_id = $2 AND site_id = $3 AND released_at IS NULL
            RETURNING {Columns}
            """, restrictionId, tenantId, siteId, actorId))
        await using (var r = await release.ExecuteReaderAsync(ct))
        {
            if (!await r.ReadAsync(ct))
            {
                await r.DisposeAsync();
                await txScope.RollbackAsync(ct);
                return null;
            }

            released = Map(r);
        }

        // Only bays this restriction itself closed are reopened, and only when nothing else still
        // covers them (another live restriction or an acknowledged maintenance window at now()).
        if (released.AffectedSlotIds.Count > 0)
            await RestoreSlotsAsync(db, tx, tenantId, siteId, released.AffectedSlotIds, ct);

        await AuditAsync(db, tx, tenantId, siteId, actorId, actorRole,
            "EMERGENCY_RESTRICTION_RELEASED", released.Scope, released.Id, released.Reason, ct);

        var eventId = Guid.NewGuid();
        var occurredAt = _clock.GetUtcNow();
        await txScope.StageActionAsync(async token =>
        {
            if (layoutInvalidations is not null)
                await layoutInvalidations.StageAsync(tenantId, siteId,
                    [
                        LayoutInvalidationResources.Structure,
                        LayoutInvalidationResources.FullLayout,
                        LayoutInvalidationResources.CapacitySummary
                    ], token);

            if (restrictionsPublisher is not null)
                await restrictionsPublisher.StageReleasedAsync(
                    new FacilityCapacityRestrictionReleasedV1(
                        eventId,
                        FacilityCapacityRestrictionTopology.CurrentSchemaVersion,
                        occurredAt,
                        tenantId,
                        siteId,
                        released.Id,
                        released.AffectedSlotIds),
                    token);
        }, ct);

        await txScope.CommitWithoutStagingAsync(ct);
        return released;
    }

    public async Task<int> ExpireDueAsync(DateTimeOffset now, CancellationToken ct)
    {
        await using var txScope = await FacilityOutboxTransaction.BeginAsync(source, context, ct);
        var db = txScope.Connection;
        var tx = txScope.Transaction;

        // FOR UPDATE SKIP LOCKED lets concurrent replicas claim disjoint rows, so a due restriction
        // is closed and its bays restored exactly once even when several ticks overlap.
        var expired = new List<EmergencyRestriction>();
        await using (var claim = Command(db, tx, $"""
            UPDATE structure_emergency_restrictions
            SET released_at = $1, released_by = NULL
            WHERE id IN (
                SELECT id
                FROM structure_emergency_restrictions
                WHERE released_at IS NULL
                  AND effective_until IS NOT NULL
                  AND effective_until <= $1
                ORDER BY effective_until
                FOR UPDATE SKIP LOCKED
            )
            RETURNING {Columns}
            """, now))
        await using (var reader = await claim.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct)) expired.Add(Map(reader));
        }

        // Nothing expired: never write an empty outbox record.
        if (expired.Count == 0)
        {
            await txScope.RollbackAsync(ct);
            return 0;
        }

        foreach (var item in expired)
        {
            if (item.AffectedSlotIds.Count > 0)
                await RestoreSlotsAsync(db, tx, item.TenantId, item.SiteId, item.AffectedSlotIds, ct);
            await AuditAsync(db, tx, item.TenantId, item.SiteId, actorId: null, "system",
                "EMERGENCY_RESTRICTION_EXPIRED", item.Scope, item.Id, item.Reason, ct);
        }

        var occurredAt = _clock.GetUtcNow();
        var sites = new HashSet<(Guid TenantId, Guid SiteId)>();
        foreach (var item in expired) sites.Add((item.TenantId, item.SiteId));
        await txScope.StageActionAsync(async token =>
        {
            foreach (var (siteTenantId, siteSiteId) in sites)
            {
                if (layoutInvalidations is not null)
                    await layoutInvalidations.StageAsync(siteTenantId, siteSiteId,
                        [
                            LayoutInvalidationResources.Structure,
                            LayoutInvalidationResources.FullLayout,
                            LayoutInvalidationResources.CapacitySummary
                        ], token);
            }

            if (restrictionsPublisher is not null)
            {
                foreach (var item in expired)
                    await restrictionsPublisher.StageReleasedAsync(
                        new FacilityCapacityRestrictionReleasedV1(
                            Guid.NewGuid(),
                            FacilityCapacityRestrictionTopology.CurrentSchemaVersion,
                            occurredAt,
                            item.TenantId,
                            item.SiteId,
                            item.Id,
                            item.AffectedSlotIds),
                        token);
            }
        }, ct);

        await txScope.CommitWithoutStagingAsync(ct);
        return expired.Count;
    }

    // Reopens only the bays a restriction itself closed, and only when nothing else still covers them
    // (another live restriction or an acknowledged maintenance window at now()). Shared by the manual
    // ReleaseAsync path and the automatic ExpireDueAsync path so the SQL exists exactly once.
    private static async Task RestoreSlotsAsync(
        NpgsqlConnection db,
        NpgsqlTransaction tx,
        Guid tenantId,
        Guid siteId,
        IReadOnlyList<Guid> affectedSlotIds,
        CancellationToken ct)
    {
        await using var restore = Command(db, tx, """
            UPDATE parking_slots slot
            SET physical_state = 'AVAILABLE', updated_at = now()
            WHERE slot.tenant_id = $1 AND slot.site_id = $2 AND slot.id = ANY($3::uuid[])
              AND slot.deleted_at IS NULL
              AND slot.physical_state IN ('MAINTENANCE', 'UNAVAILABLE')
              AND NOT EXISTS (
                  SELECT 1 FROM structure_emergency_restrictions other
                  JOIN spatial_units slot_unit ON slot_unit.id = slot.spatial_unit_id
                  LEFT JOIN spatial_units other_unit
                      ON other_unit.id = other.unit_id AND other_unit.deleted_at IS NULL
                  WHERE other.tenant_id = $1 AND other.site_id = $2
                    AND other.released_at IS NULL
                    AND (other.effective_until IS NULL OR other.effective_until > now())
                    AND (
                        other.scope = 'SITE'
                        OR other.slot_id = slot.id
                        OR (other.scope = 'UNIT' AND other_unit.id IS NOT NULL
                            AND slot_unit.path LIKE other_unit.path || '%')
                    )
              )
              AND NOT EXISTS (
                  SELECT 1 FROM structure_maintenance_windows mw
                  JOIN spatial_units slot_unit ON slot_unit.id = slot.spatial_unit_id
                  LEFT JOIN spatial_units mw_unit
                      ON mw_unit.id = mw.unit_id AND mw_unit.deleted_at IS NULL
                  WHERE mw.site_id = $2 AND mw.tenant_id = $1
                    AND mw.cancelled_at IS NULL
                    AND mw.enforcement_state = 'acknowledged'
                    AND mw.service_window @> now()
                    AND (
                        (mw.unit_id IS NULL AND mw.slot_id IS NULL)
                        OR mw.slot_id = slot.id
                        OR (mw.slot_id IS NULL AND mw.unit_id IS NOT NULL
                            AND mw_unit.id IS NOT NULL
                            AND slot_unit.path LIKE mw_unit.path || '%')
                    )
              )
            """, tenantId, siteId, AddUuids(affectedSlotIds));
        await restore.ExecuteNonQueryAsync(ct);
    }

    private static async Task<List<Guid>> ResolveAffectedSlotsAsync(
        NpgsqlConnection db,
        NpgsqlTransaction tx,
        Guid tenantId,
        Guid siteId,
        string scope,
        Guid? unitId,
        Guid? slotId,
        CancellationToken ct)
    {
        var sql = scope switch
        {
            EmergencyRestrictionRules.UnitScope => """
                SELECT slot.id
                FROM parking_slots slot
                JOIN spatial_units subtree ON subtree.id = slot.spatial_unit_id AND subtree.deleted_at IS NULL
                JOIN spatial_units target
                    ON target.id = $3 AND target.site_id = $2 AND target.tenant_id = $1
                   AND target.deleted_at IS NULL
                WHERE slot.tenant_id = $1 AND slot.site_id = $2 AND slot.deleted_at IS NULL
                  AND subtree.path LIKE target.path || '%'
                ORDER BY slot.slot_code
                """,
            EmergencyRestrictionRules.SlotScope => """
                SELECT slot.id
                FROM parking_slots slot
                WHERE slot.tenant_id = $1 AND slot.site_id = $2 AND slot.id = $3 AND slot.deleted_at IS NULL
                """,
            _ => """
                SELECT slot.id
                FROM parking_slots slot
                WHERE slot.tenant_id = $1 AND slot.site_id = $2 AND slot.deleted_at IS NULL
                ORDER BY slot.slot_code
                """
        };

        var scopeTarget = scope == EmergencyRestrictionRules.SlotScope ? slotId : unitId;
        // The site-wide statement only references $1 and $2; binding a third parameter would make
        // PostgreSQL reject the command with 08P01 at execution time.
        await using var cmd = scope == EmergencyRestrictionRules.SiteScope
            ? Command(db, tx, sql, tenantId, siteId)
            : Command(db, tx, sql, tenantId, siteId, scopeTarget);
        var ids = new List<Guid>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) ids.Add(r.GetGuid(0));

        if (ids.Count == 0 && slotId is { } explicitSlot) ids.Add(explicitSlot);
        return ids;
    }

    private static async Task EnsureSiteAsync(
        NpgsqlConnection db, NpgsqlTransaction tx, Guid tenantId, Guid siteId, CancellationToken ct)
    {
        await using var cmd = Command(db, tx,
            "SELECT 1 FROM parking_sites WHERE id=$1 AND tenant_id=$2 AND deleted_at IS NULL",
            siteId, tenantId);
        if (await cmd.ExecuteScalarAsync(ct) is null)
            throw new StructureException("SITE_NOT_FOUND", "Parking site was not found.");
    }

    private static async Task EnsureUnitAsync(
        NpgsqlConnection db, NpgsqlTransaction tx, Guid tenantId, Guid siteId, Guid unitId, CancellationToken ct)
    {
        await using var cmd = Command(db, tx, """
            SELECT 1 FROM spatial_units
            WHERE id=$1 AND site_id=$2 AND tenant_id=$3 AND deleted_at IS NULL
            """, unitId, siteId, tenantId);
        if (await cmd.ExecuteScalarAsync(ct) is null)
            throw new StructureException("UNIT_NOT_FOUND", "Spatial unit does not belong to this site.");
    }

    // The layout exposes Floor/Zone ids while the slot row points at its leaf ROW unit, so an
    // operator may legitimately name any ancestor. The slot's real leaf unit is returned so the
    // restriction stores a unit id that the admission and release queries can match exactly.
    private static async Task<Guid> EnsureSlotAsync(
        NpgsqlConnection db, NpgsqlTransaction tx, Guid tenantId, Guid siteId, Guid unitId, Guid slotId,
        CancellationToken ct)
    {
        await using var cmd = Command(db, tx, """
            SELECT slot.spatial_unit_id
            FROM parking_slots slot
            JOIN spatial_units slot_unit ON slot_unit.id = slot.spatial_unit_id AND slot_unit.deleted_at IS NULL
            JOIN spatial_units target_unit
                ON target_unit.id = $2 AND target_unit.site_id = $3 AND target_unit.tenant_id = $4
               AND target_unit.deleted_at IS NULL
            WHERE slot.id = $1 AND slot.site_id = $3 AND slot.tenant_id = $4 AND slot.deleted_at IS NULL
              AND (slot.spatial_unit_id = $2 OR slot_unit.path LIKE target_unit.path || '%')
            """, slotId, unitId, siteId, tenantId);
        if (await cmd.ExecuteScalarAsync(ct) is not Guid actualUnitId)
            throw new StructureException("SLOT_NOT_FOUND",
                "Slot does not belong to the specified unit in this site.");
        return actualUnitId;
    }

    private static async Task AuditAsync(
        NpgsqlConnection db,
        NpgsqlTransaction tx,
        Guid tenantId,
        Guid siteId,
        Guid? actorId,
        string actorRole,
        string action,
        string targetEntity,
        Guid targetId,
        string reason,
        CancellationToken ct)
    {
        await using var cmd = Command(db, tx, """
            INSERT INTO operator_audit_logs
                (id, tenant_id, site_id, actor_id, actor_role, action, target_entity, target_id, reason)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9)
            """, Guid.NewGuid(), tenantId, siteId, actorId, actorRole, action, targetEntity, targetId, reason);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static EmergencyRestriction Map(NpgsqlDataReader r) => new(
        r.GetGuid(0),
        r.GetGuid(1),
        r.GetGuid(2),
        r.GetString(3),
        r.GetString(4),
        r.IsDBNull(5) ? null : r.GetGuid(5),
        r.IsDBNull(6) ? null : r.GetGuid(6),
        ParseSlotIds(r.IsDBNull(7) ? null : r.GetString(7)),
        r.GetString(8),
        new DateTimeOffset(DateTime.SpecifyKind(r.GetDateTime(9), DateTimeKind.Utc)),
        r.IsDBNull(10) ? null : new DateTimeOffset(DateTime.SpecifyKind(r.GetDateTime(10), DateTimeKind.Utc)),
        r.GetGuid(11),
        new DateTimeOffset(DateTime.SpecifyKind(r.GetDateTime(12), DateTimeKind.Utc)),
        r.IsDBNull(13) ? null : new DateTimeOffset(DateTime.SpecifyKind(r.GetDateTime(13), DateTimeKind.Utc)),
        r.IsDBNull(14) ? null : r.GetGuid(14));

    private static IReadOnlyList<Guid> ParseSlotIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static NpgsqlParameter AddUuids(IReadOnlyList<Guid> values) => new()
    {
        NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Uuid,
        Value = values.ToArray()
    };

    private static NpgsqlCommand Command(
        NpgsqlConnection db,
        NpgsqlTransaction? tx,
        string sql,
        params object?[] values)
    {
        var cmd = db.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        foreach (var value in values)
            cmd.Parameters.Add(value is NpgsqlParameter parameter
                ? parameter
                : new NpgsqlParameter { Value = value ?? DBNull.Value });
        return cmd;
    }
}