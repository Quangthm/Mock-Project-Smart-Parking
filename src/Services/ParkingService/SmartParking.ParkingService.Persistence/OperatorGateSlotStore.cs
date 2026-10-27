using Npgsql;
using Microsoft.EntityFrameworkCore;
using SmartParking.ParkingService.Domain;

namespace SmartParking.ParkingService.Persistence;

public sealed record OperatorGateSlot(Guid Id, string Code, string VehicleType);

public sealed class OperatorGateSlotStore(NpgsqlDataSource source, ParkingDbContext db)
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
        await using var db = await source.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        await using var command = new NpgsqlCommand("""
            WITH candidate AS (
                SELECT id
                FROM parking_slots
                WHERE tenant_id = $1 AND site_id = $2 AND deleted_at IS NULL
                  AND physical_state = 'AVAILABLE'
                  AND (reservation_state IS NULL OR ($6 AND reservation_state = 'RESERVED'))
                  AND ($3 = 'ANY' OR supported_vehicle_type = $3)
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
        var slot = new OperatorGateSlot(reader.GetGuid(0), reader.GetString(1), reader.GetString(2));
        await reader.CloseAsync();
        await tx.CommitAsync(ct);
        return slot;
    }

    public async Task ReleaseAsync(Guid tenantId, Guid siteId, Guid slotId, CancellationToken ct)
    {
        await db.ParkingSlotsFull.Where(x => x.TenantId == tenantId && x.SiteId == siteId &&
            x.Id == slotId && x.PhysicalState == "OCCUPIED")
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.PhysicalState, "AVAILABLE")
                .SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow), ct);
    }
}
