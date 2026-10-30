using System.Data;
using System.Text.Json;
using Npgsql;
using SmartParking.FacilityService.Application;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Persistence;

// Read-only projection. Never takes the site advisory lock or writes synchronization rows.
public sealed class PostgresOperationalLayoutReader(NpgsqlDataSource source) : IOperationalLayoutReader
{
    private static NpgsqlCommand Command(NpgsqlConnection db, NpgsqlTransaction tx, string sql, params object?[] values)
    {
        var command = new NpgsqlCommand(sql, db, tx);
        foreach (var value in values) command.Parameters.Add(new NpgsqlParameter { Value = value ?? DBNull.Value });
        return command;
    }

    private sealed record UnitRow(Guid Id, Guid? ParentId, UnitType Type, string Name, int MaxCapacity);
    private sealed record SlotRow(
        Guid Id, Guid UnitId, string Code, string? VehicleType, string? SlotType,
        string? PhysicalState, string? ReservationState, string? Coordinates3D, string? Features,
        DateTimeOffset UpdatedAt);
    private sealed record ProjectedSlot(Guid UnitId, SlotVisualStateDto Dto);

    public async Task<OperationalLayoutReadResult> ReadAsync(
        LayoutReadScope scope, Guid siteId, VehicleType? vehicleType, string? floor, CancellationToken ct)
    {
        await using var db = await source.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);

        Guid siteKey; Guid siteTenantId; string siteCode; string siteName;
        await using (var cmd = Command(db, tx, """
            SELECT s.id, s.tenant_id, s.site_code, s.name
            FROM parking_sites s
            JOIN tenants t ON t.id = s.tenant_id
            WHERE s.id = $1
              AND s.deleted_at IS NULL
              AND ($2::uuid[] IS NULL OR s.tenant_id = ANY($2))
              AND ($3::boolean OR s.status = 'ACTIVE')
              AND t.status = 'ACTIVE'
              AND t.deleted_at IS NULL
            """, siteId, scope.TenantIds?.ToArray(), scope.IncludeInactiveSites))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            if (!await r.ReadAsync(ct))
                throw new StructureException("SITE_NOT_FOUND", "Parking site was not found in this tenant.");
            siteKey = r.GetGuid(0);
            siteTenantId = r.GetGuid(1);
            siteCode = r.GetString(2);
            siteName = r.GetString(3);
        }

        var units = new List<UnitRow>();
        await using (var cmd = Command(db, tx,
            "SELECT id, parent_id, unit_type, name, max_capacity FROM spatial_units WHERE site_id=$1 AND tenant_id=$2 AND deleted_at IS NULL",
            siteId, siteTenantId))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct))
                units.Add(new UnitRow(
                    r.GetGuid(0),
                    r.IsDBNull(1) ? null : r.GetGuid(1),
                    Enum.TryParse<UnitType>(r.GetString(2), out var type) ? type : UnitType.FLOOR,
                    r.GetString(3),
                    r.GetInt32(4)));
        var unitById = units.ToDictionary(unit => unit.Id);

        var rows = new List<SlotRow>();
        // jsonb is read as its text representation and parsed in C# so the projection
        // emits JSON objects/values rather than JSON strings.
        await using (var cmd = Command(db, tx, """
            SELECT id, spatial_unit_id, slot_code, supported_vehicle_type, slot_type,
                   physical_state, reservation_state, coordinates_3d, features, updated_at
            FROM parking_slots
            WHERE site_id=$1 AND tenant_id=$2 AND deleted_at IS NULL
            ORDER BY slot_code
            """, siteId, siteTenantId))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct))
                rows.Add(new SlotRow(
                    r.GetGuid(0), r.GetGuid(1), r.GetString(2),
                    r.IsDBNull(3) ? null : r.GetString(3),
                    r.IsDBNull(4) ? null : r.GetString(4),
                    r.IsDBNull(5) ? null : r.GetString(5),
                    r.IsDBNull(6) ? null : r.GetString(6),
                    r.IsDBNull(7) ? null : r.GetString(7),
                    r.IsDBNull(8) ? null : r.GetString(8),
                    AsUtc(r.GetDateTime(9))));

        int motorcycleMaxCapacity; int motorcycleCapacityRows;
        await using (var cmd = Command(db, tx, """
            SELECT COALESCE(SUM(c.max_capacity), 0)::int, COUNT(*)::int
            FROM spatial_unit_capacities c
            JOIN spatial_units u ON u.id = c.spatial_unit_id AND u.deleted_at IS NULL
            WHERE c.site_id=$1 AND c.tenant_id=$2 AND c.vehicle_type='MOTORCYCLE'
            """, siteId, siteTenantId))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            await r.ReadAsync(ct);
            motorcycleMaxCapacity = r.GetInt32(0);
            motorcycleCapacityRows = r.GetInt32(1);
        }

        await tx.CommitAsync(ct);

        var floors = units
            .Where(unit => unit.Type == UnitType.FLOOR)
            // OPEN (D0-5): no floor level column exists; level is the 1-based name-ascending ordinal.
            .OrderBy(unit => unit.Name, StringComparer.Ordinal)
            .Select((unit, index) => new FloorProjectionDto(unit.Id, unit.Name, unit.Name, index + 1, unit.MaxCapacity))
            .ToArray();

        var projected = rows
            .Select(row => new ProjectedSlot(row.UnitId, Project(row, unitById)))
            .ToArray();

        // Post-mask counts are computed over the full unfiltered row set. The filtered slots[]
        // is only a display subset, so computing these after filtering would leak BACKUP counts
        // and would change metrics based on query parameters.
        var maskedReserved = projected
            .Where(slot => slot.Dto.ReservationState is ReservationState.RESERVED or ReservationState.BACKUP)
            .ToArray();
        var maskedReservedCount = maskedReserved.Length;
        var maskedZoneReservedCounts = maskedReserved
            .Where(slot => slot.Dto.ZoneCode is not null)
            .GroupBy(slot => slot.Dto.ZoneCode!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        var totalPhysicalCapacity = projected.Count(slot => slot.Dto.SupportedVehicleType == VehicleType.CAR);
        var occupiedCount = projected.Count(slot => slot.Dto.PhysicalState == PhysicalState.OCCUPIED);
        var reservedCount = projected.Count(slot => slot.Dto.ReservationState == ReservationState.RESERVED);
        var availableCarSlots = projected.Count(slot =>
            slot.Dto.SupportedVehicleType == VehicleType.CAR &&
            slot.Dto.PhysicalState == PhysicalState.AVAILABLE &&
            slot.Dto.ReservationState == ReservationState.UNRESERVED);
        // Rule 15: if no MOTORCYCLE capacity row exists, emit 0/0/0 instead of throwing.
        var motorcycleOccupied = motorcycleCapacityRows == 0
            ? 0
            : projected.Count(slot =>
                slot.Dto.SupportedVehicleType == VehicleType.MOTORCYCLE &&
                slot.Dto.PhysicalState == PhysicalState.OCCUPIED);

        var zoneCapacities = units
            .Where(unit => unit.Type == UnitType.ZONE)
            .OrderBy(unit => unit.Name, StringComparer.Ordinal)
            .Select(zone =>
            {
                var inZone = projected.Where(slot => IsInSubtree(unitById, slot.UnitId, zone.Id)).ToArray();
                var occupied = inZone.Count(slot => slot.Dto.PhysicalState == PhysicalState.OCCUPIED);
                // OPEN (D0-5): approximated by subtree RESERVED count — no zone-level allocation source.
                var reserved = inZone.Count(slot => slot.Dto.ReservationState == ReservationState.RESERVED);
                var floorUnit = Ancestor(unitById, zone.Id, UnitType.FLOOR);
                return new ZoneCapacityDto(
                    zone.Id, zone.Name, floorUnit?.Name ?? string.Empty, zone.Name,
                    zone.MaxCapacity, occupied,
                    // OPEN (D0-5): approximated by subtree RESERVED count — no zone-level allocation source.
                    reserved, reserved);
            })
            .ToArray();

        IEnumerable<SlotVisualStateDto> filtered = projected.Select(slot => slot.Dto);
        if (vehicleType is { } requestedVehicle)
            filtered = filtered.Where(slot => slot.SupportedVehicleType == requestedVehicle);
        if (!string.IsNullOrWhiteSpace(floor))
        {
            if (!floors.Any(item => string.Equals(item.FloorCode, floor, StringComparison.OrdinalIgnoreCase)))
                throw new StructureException("INVALID_FLOOR", "Unknown floor code.");
            filtered = filtered.Where(slot => string.Equals(slot.FloorCode, floor, StringComparison.OrdinalIgnoreCase));
        }

        return new OperationalLayoutReadResult(
            new OperationalLayoutProjectionDto(
                siteKey,
                siteCode,
                siteName,
                OperationalLayoutContract.Version,
                DateTimeOffset.UtcNow,
                // OPEN (R3): prototype until a freshness source exists.
                true,
                OperationalLayoutContract.StalenessThresholdSeconds,
                new LayoutMetricsDto(
                    totalPhysicalCapacity,
                    occupiedCount,
                    reservedCount,
                    availableCarSlots,
                    new MotorcycleCapacityDto(
                        motorcycleMaxCapacity,
                        motorcycleOccupied,
                        Math.Max(0, motorcycleMaxCapacity - motorcycleOccupied)),
                    zoneCapacities),
                floors,
                filtered.ToArray()),
            maskedReservedCount,
            maskedZoneReservedCounts);
    }

    private static SlotVisualStateDto Project(SlotRow row, IReadOnlyDictionary<Guid, UnitRow> units)
    {
        var physicalState = OperationalStateMapping.MapPhysicalState(row.PhysicalState);
        var reservationState = OperationalStateMapping.MapReservationState(row.ReservationState);
        var slotType = Enum.TryParse<SlotType>(row.SlotType, out var parsedType) && Enum.IsDefined(parsedType)
            ? parsedType
            : SlotType.STANDARD;
        // OPEN: supported_vehicle_type is nullable in the schema and the contract has no
        // UNKNOWN member. OVERSIZED keeps a missing/unknown type out of car capacity.
        var vehicleType = Enum.TryParse<VehicleType>(row.VehicleType, out var parsedVehicle) && Enum.IsDefined(parsedVehicle)
            ? parsedVehicle
            : VehicleType.OVERSIZED;

        var floor = Ancestor(units, row.UnitId, UnitType.FLOOR);
        var zone = Ancestor(units, row.UnitId, UnitType.ZONE);
        // OPEN: no row column exists; nearest BLOCK ancestor name is the documented approximation.
        var block = Ancestor(units, row.UnitId, UnitType.BLOCK);

        return new SlotVisualStateDto(
            row.Id,
            row.Code,
            floor?.Id ?? Guid.Empty,
            floor?.Name ?? string.Empty,
            zone?.Name,
            block?.Name,
            vehicleType,
            slotType,
            physicalState,
            reservationState,
            // No session_type column exists in FacilityService.
            null,
            slotType == SlotType.EV && physicalState == PhysicalState.OCCUPIED,
            physicalState == PhysicalState.AVAILABLE && reservationState == ReservationState.UNRESERVED,
            ParseJson(row.Coordinates3D),
            ParseJson(row.Features),
            // No session/plate source in FacilityService for Phase 1a.
            null,
            row.UpdatedAt);
    }

    private static JsonElement? ParseJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static UnitRow? Ancestor(
        IReadOnlyDictionary<Guid, UnitRow> units, Guid unitId, UnitType type)
    {
        var current = unitId;
        for (var depth = 0; depth < 64 && units.TryGetValue(current, out var unit); depth++)
        {
            if (unit.Type == type) return unit;
            if (unit.ParentId is not { } parent) return null;
            current = parent;
        }
        return null;
    }

    private static bool IsInSubtree(
        IReadOnlyDictionary<Guid, UnitRow> units, Guid unitId, Guid ancestorId)
    {
        var current = unitId;
        for (var depth = 0; depth < 64 && units.TryGetValue(current, out var unit); depth++)
        {
            if (unit.Id == ancestorId) return true;
            if (unit.ParentId is not { } parent) return false;
            current = parent;
        }
        return false;
    }

    private static DateTimeOffset AsUtc(DateTime value)
        => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
