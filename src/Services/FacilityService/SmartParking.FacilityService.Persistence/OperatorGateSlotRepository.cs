using Npgsql;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SmartParking.FacilityService.Application;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Persistence;

public sealed class OperatorGateSlotRepository(ParkingDbContext context) : IOperatorGateSlotRepository
{
    public async Task<OperatorGateSlot> ClaimAvailableAsync(
        Guid tenantId,
        Guid siteId,
        string vehicleType,
        Guid? preferredSlotId,
        Guid? targetUnitId,
        bool allowReservedSlot,
        CancellationToken ct)
    {
        var (db, tx) = OwningTransaction();
        await ReservationDependencyGuard.RequireAvailableAsync(db, tx, siteId, ct);
        await using var command = new NpgsqlCommand("""
            WITH candidate AS (
                SELECT id
                FROM parking_slots
                WHERE tenant_id = $1 AND site_id = $2 AND deleted_at IS NULL
                  AND physical_state = 'AVAILABLE'
                  AND (reservation_state IS NULL OR ($6 AND reservation_state = 'RESERVED'))
                  AND ($3 = 'ANY'
                       OR EXISTS (
                           SELECT 1 FROM slot_compatibilities compatibility
                           WHERE compatibility.slot_id = parking_slots.id AND compatibility.vehicle_type = $3
                       )
                       OR (supported_vehicle_type = $3
                           AND NOT EXISTS (
                               SELECT 1 FROM slot_compatibilities compatibility
                               WHERE compatibility.slot_id = parking_slots.id
                           )))
                  AND ($4::uuid IS NULL OR id = $4)
                  AND ($5::uuid IS NULL OR EXISTS (
                      SELECT 1
                      FROM spatial_units requested
                      JOIN spatial_units slot_unit ON slot_unit.id = parking_slots.spatial_unit_id
                      WHERE requested.id = $5 AND requested.site_id = $2
                        AND slot_unit.site_id = $2 AND slot_unit.path LIKE requested.path || '%'
                  ))
                  AND EXISTS (
                      SELECT 1
                      FROM parking_sites site
                      JOIN tenants tenant ON tenant.id = site.tenant_id
                      WHERE site.id = parking_slots.site_id
                        AND site.tenant_id = parking_slots.tenant_id
                        AND site.status = 'ACTIVE' AND site.deleted_at IS NULL
                        AND tenant.status = 'ACTIVE' AND tenant.deleted_at IS NULL
                  )
                  -- Walk-in entry is physical and immediate, so slots covered right now by an
                  -- acknowledged closure must not be handed to a vehicle. Future reservations stay
                  -- admissible because Operations enforces the [start, end) overlap authoritatively.
                  AND NOT EXISTS (
                      SELECT 1
                      FROM structure_maintenance_windows mw
                      JOIN spatial_units slot_unit ON slot_unit.id = parking_slots.spatial_unit_id
                      LEFT JOIN spatial_units mw_unit ON mw_unit.id = mw.unit_id AND mw_unit.deleted_at IS NULL
                      WHERE mw.site_id = parking_slots.site_id
                        AND mw.tenant_id = parking_slots.tenant_id
                        AND mw.cancelled_at IS NULL
                        AND mw.enforcement_state = 'acknowledged'
                        AND mw.service_window @> now()
                        AND (
                            (mw.unit_id IS NULL AND mw.slot_id IS NULL)
                            OR mw.slot_id = parking_slots.id
                            OR (mw.slot_id IS NULL AND mw.unit_id IS NOT NULL AND mw_unit.id IS NOT NULL AND slot_unit.path LIKE mw_unit.path || '%')
                        )
                  )
                  -- Emergency restrictions recorded by the operator block new physical entry immediately,
                  -- independently of whether Operations reconciliation has finished.
                  AND NOT EXISTS (
                      SELECT 1
                      FROM structure_emergency_restrictions restriction
                      LEFT JOIN spatial_units restricted_unit
                          ON restricted_unit.id = restriction.unit_id AND restricted_unit.deleted_at IS NULL
                      JOIN spatial_units entry_unit ON entry_unit.id = parking_slots.spatial_unit_id
                      WHERE restriction.site_id = parking_slots.site_id
                        AND restriction.tenant_id = parking_slots.tenant_id
                        AND restriction.released_at IS NULL
                        AND (restriction.effective_until IS NULL OR restriction.effective_until > now())
                        AND (
                            restriction.scope = 'SITE'
                            OR restriction.slot_id = parking_slots.id
                            OR (restriction.scope = 'UNIT' AND restricted_unit.id IS NOT NULL
                                AND entry_unit.path LIKE restricted_unit.path || '%')
                        )
                  )
                ORDER BY slot_code
                LIMIT 1
                FOR UPDATE SKIP LOCKED
            )
            UPDATE parking_slots slot
            SET physical_state = 'OCCUPIED', reservation_state = NULL,
                updated_at = now()
            FROM candidate
            WHERE slot.id = candidate.id
            RETURNING slot.id, slot.slot_code, slot.supported_vehicle_type
            """, db, tx);
        command.Parameters.AddWithValue(tenantId);
        command.Parameters.AddWithValue(siteId);
        command.Parameters.AddWithValue(vehicleType);
        command.Parameters.AddWithValue((object?)preferredSlotId ?? DBNull.Value);
        command.Parameters.AddWithValue((object?)targetUnitId ?? DBNull.Value);
        command.Parameters.AddWithValue(allowReservedSlot);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            throw new StructureException("NO_AVAILABLE_SLOT", "No eligible parking slot is available for a walk-in vehicle.");
        var slot = new OperatorGateSlot(reader.GetGuid(0), reader.GetString(1),
            reader.IsDBNull(2) ? string.Empty : reader.GetString(2));
        await reader.CloseAsync();
        return slot;
    }

    public async Task<bool> ReleaseAsync(Guid tenantId, Guid siteId, Guid slotId, CancellationToken ct)
    {
        var (db, tx) = OwningTransaction();
        await ReservationDependencyGuard.RequireAvailableAsync(db, tx, siteId, ct);
        await using var command = new NpgsqlCommand("""
            UPDATE parking_slots
            SET physical_state = 'AVAILABLE', updated_at = now()
            WHERE tenant_id = $1 AND site_id = $2 AND id = $3
              AND physical_state = 'OCCUPIED'
            """, db, tx);
        command.Parameters.AddWithValue(tenantId);
        command.Parameters.AddWithValue(siteId);
        command.Parameters.AddWithValue(slotId);
        return await command.ExecuteNonQueryAsync(ct) > 0;
    }

    private (NpgsqlConnection, NpgsqlTransaction) OwningTransaction()
    {
        var transaction = context.Database.CurrentTransaction
            ?? throw new InvalidOperationException("Gate changes require the owning UoW transaction.");
        return ((NpgsqlConnection)context.Database.GetDbConnection(),
            (NpgsqlTransaction)transaction.GetDbTransaction());
    }
}
