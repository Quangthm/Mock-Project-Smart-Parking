using Npgsql;
using SmartParking.ParkingService.Application;
using SmartParking.ParkingService.Domain;

namespace SmartParking.ParkingService.Persistence;

// One transaction and site row lock per mutation. Register as scoped; NpgsqlDataSource is singleton.
public sealed class PostgresParkingStructureRepository(NpgsqlDataSource source, IOwnerAuthorizer identity, IStructureCommitments commitments) : IParkingStructureRepository
{
    private static NpgsqlCommand Command(NpgsqlConnection db, NpgsqlTransaction tx, string sql, params object?[] values)
    {
        var command = new NpgsqlCommand(sql, db, tx);
        foreach (var value in values) command.Parameters.Add(new NpgsqlParameter { Value = value ?? DBNull.Value });
        return command;
    }
    private static async Task Execute(NpgsqlConnection db, NpgsqlTransaction tx, string sql, CancellationToken ct, params object?[] values)
    { await using var cmd = Command(db, tx, sql, values); await cmd.ExecuteNonQueryAsync(ct); }
    private async Task Authorize(NpgsqlConnection db, NpgsqlTransaction tx, OwnerScope owner, CancellationToken ct)
    {
        if (!await identity.IsOwnerAsync(owner, ct))
            throw new StructureException("FORBIDDEN", "Active Owner membership is required.");
        await using var cmd = Command(db, tx, "SELECT id FROM tenants WHERE id=$1 AND status='ACTIVE' AND deleted_at IS NULL FOR SHARE", owner.TenantId);
        if (await cmd.ExecuteScalarAsync(ct) is null)
            throw new StructureException("FORBIDDEN", "Active tenant is required.");
    }
    private static SiteProfile Profile(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetString(4),
        r.IsDBNull(5) ? null : r.GetDecimal(5), r.IsDBNull(6) ? null : r.GetDecimal(6), r.GetString(7) == "ACTIVE", r.GetInt32(8), r.GetString(7));
    private const string SiteColumns = "id, tenant_id, site_code, name, address, latitude, longitude, status, total_physical_capacity";

    public async Task<IReadOnlyList<SiteProfile>> ListAsync(OwnerScope owner, CancellationToken ct)
    {
        await using var db = await source.OpenConnectionAsync(ct); await using var tx = await db.BeginTransactionAsync(ct);
        await Authorize(db, tx, owner, ct);
        var result = new List<SiteProfile>();
        await using (var cmd = Command(db, tx, $"SELECT {SiteColumns} FROM parking_sites WHERE tenant_id = $1 AND deleted_at IS NULL ORDER BY site_code", owner.TenantId))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct)) result.Add(Profile(reader));
        await tx.CommitAsync(ct); return result;
    }
    public async Task<SiteProfile> CreateAsync(OwnerScope owner, string code, string name, string address, decimal? latitude, decimal? longitude, CancellationToken ct)
    {
        var aggregate = new ParkingStructure(new(Guid.NewGuid(), owner.TenantId, "", "", "", null, null, true, 0));
        aggregate.UpdateProfile(code, name, address, latitude, longitude); var site = aggregate.Site;
        await using var db = await source.OpenConnectionAsync(ct); await using var tx = await db.BeginTransactionAsync(ct);
        await Authorize(db, tx, owner, ct);
        try
        {
            await Execute(db, tx, """
                INSERT INTO parking_sites(id, tenant_id, site_code, name, address, latitude, longitude)
                VALUES ($1, $2, $3, $4, $5, $6::numeric, $7::numeric)
                """, ct, site.Id, site.TenantId, site.Code, site.Name, site.Address, site.Latitude, site.Longitude);
            await tx.CommitAsync(ct); return site;
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
        { throw new StructureException("DUPLICATE_IDENTIFIER", "Site code already exists in this tenant."); }
    }
    public async Task<ParkingStructure> ReadAsync(OwnerScope owner, Guid siteId, CancellationToken ct)
    {
        await using var db = await source.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        await Authorize(db, tx, owner, ct); var structure = await Load(db, tx, owner.TenantId, siteId, false, ct);
        await tx.CommitAsync(ct); return structure;
    }
    public async Task<T> ChangeAsync<T>(OwnerScope owner, Guid siteId, Func<ParkingStructure, T> change, CancellationToken ct)
    {
        await using var db = await source.OpenConnectionAsync(ct); await using var tx = await db.BeginTransactionAsync(ct);
        await Authorize(db, tx, owner, ct);
        // Hold the Reservation-owned durable fence until this local transaction has completed.
        await using var lease = await commitments.AcquireAsync(owner.TenantId, siteId, ct);
        var structure = await Load(db, tx, owner.TenantId, siteId, true, ct, lease.Snapshot);
        if (structure.Site.TotalPhysicalCapacity != structure.Slots.Count)
            throw new StructureException("STRUCTURE_CONFLICT", "Declared physical capacity differs from active slots. Reconcile the existing layout before editing.");
        var oldUnits = structure.Units.Select(u => u.Id).ToHashSet();
        var oldSlots = structure.Slots.Select(s => s.Id).ToHashSet();
        var oldPaths = structure.Paths.Select(p => p.Id).ToHashSet();
        var result = change(structure);
        try
        {
            var site = structure.Site;
            await Execute(db, tx, """
                UPDATE parking_sites SET site_code=$2, name=$3, address=$4, latitude=$5::numeric, longitude=$6::numeric,
                status=$7, total_physical_capacity=$8, updated_at=CURRENT_TIMESTAMP WHERE id=$1
                """, ct, site.Id, site.Code, site.Name, site.Address, site.Latitude, site.Longitude, site.Status, structure.Slots.Count);
            // Children are soft-deleted before their parents. No historical reference is cascaded away.
            foreach (var id in oldPaths.Except(structure.Paths.Select(p => p.Id)))
                await Execute(db, tx, "UPDATE parking_access_paths SET deleted_at=CURRENT_TIMESTAMP WHERE id=$1", ct, id);
            foreach (var id in oldSlots.Except(structure.Slots.Select(s => s.Id)))
                await Execute(db, tx, "UPDATE parking_slots SET deleted_at=CURRENT_TIMESTAMP WHERE id=$1", ct, id);
            foreach (var id in oldUnits.Except(structure.Units.Select(u => u.Id)))
                await Execute(db, tx, "UPDATE spatial_units SET deleted_at=CURRENT_TIMESTAMP WHERE id=$1", ct, id);
            foreach (var u in structure.Units.OrderBy(u => u.Path.Length))
            {
                if (oldUnits.Contains(u.Id))
                    await Execute(db, tx, "UPDATE spatial_units SET name=$2, max_capacity=$3 WHERE id=$1", ct, u.Id, u.Name, u.MaxCapacity);
                else await Execute(db, tx, """
                    INSERT INTO spatial_units(id,tenant_id,site_id,parent_id,path,unit_type,name,max_capacity)
                    VALUES ($1,$2,$3,$4::uuid,$5,$6,$7,$8)
                    """, ct, u.Id, site.TenantId, site.Id, u.ParentId, u.Path, u.Type.ToString(), u.Name, u.MaxCapacity);
            }
            foreach (var s in structure.Slots)
            {
                if (oldSlots.Contains(s.Id))
                    await Execute(db, tx, """
                        UPDATE parking_slots SET spatial_unit_id=$2, slot_code=$3, supported_vehicle_type=$4, slot_type=$5,
                        coordinates_3d=$6::jsonb, features=$7::jsonb WHERE id=$1
                        """, ct, s.Id, s.UnitId, s.Code, s.VehicleType.ToString(), s.Type.ToString(), s.Coordinates3D, s.Features);
                else await Execute(db, tx, """
                    INSERT INTO parking_slots(id,tenant_id,site_id,spatial_unit_id,slot_code,supported_vehicle_type,slot_type,coordinates_3d,features)
                    VALUES ($1,$2,$3,$4,$5,$6,$7,$8::jsonb,$9::jsonb)
                    """, ct, s.Id, site.TenantId, site.Id, s.UnitId, s.Code, s.VehicleType.ToString(), s.Type.ToString(), s.Coordinates3D, s.Features);
            }
            foreach (var p in structure.Paths.Where(p => !oldPaths.Contains(p.Id)))
                await Execute(db, tx, """
                    INSERT INTO parking_access_paths(id,tenant_id,site_id,path_code,from_unit_id,to_unit_id,map_data)
                    VALUES ($1,$2,$3,$4,$5::uuid,$6::uuid,$7::jsonb)
                    """, ct, p.Id, site.TenantId, site.Id, p.Code, p.FromUnitId, p.ToUnitId, p.MapData);
            lease.BeginCommit();
            await tx.CommitAsync(ct);
            await lease.CompleteAsync(new(site.IsActive, oldSlots.Except(structure.Slots.Select(s => s.Id)).ToArray(),
                oldUnits.Except(structure.Units.Select(u => u.Id)).ToArray(), Capacities(structure)), ct);
            return result;
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
        { throw new StructureException("DUPLICATE_IDENTIFIER", "Structure identifier already exists."); }
    }

    private static PoolCapacity[] Capacities(ParkingStructure structure)
    {
        // An ancestor unit's pool covers slots in its entire subtree.
        var result = new List<PoolCapacity>();
        foreach (var vehicle in new[] { VehicleType.CAR, VehicleType.MOTORCYCLE })
        {
            var slots = structure.Slots.Where(s => s.VehicleType == vehicle).ToArray();
            result.Add(new(null, vehicle.ToString(), slots.Length));
            foreach (var unit in structure.Units)
            {
                var descendants = structure.Units.Where(u => u.Path.StartsWith(unit.Path, StringComparison.Ordinal)).Select(u => u.Id).ToHashSet();
                result.Add(new(unit.Id, vehicle.ToString(), slots.Count(s => descendants.Contains(s.UnitId))));
            }
        }
        return result.ToArray();
    }

    private static async Task<ParkingStructure> Load(NpgsqlConnection db, NpgsqlTransaction tx, Guid tenantId, Guid siteId, bool write, CancellationToken ct, CommitmentSnapshot? snapshot = null)
    {
        SiteProfile site;
        await using (var cmd = Command(db, tx, $"SELECT {SiteColumns} FROM parking_sites WHERE id=$1 AND tenant_id=$2 AND deleted_at IS NULL" + (write ? " FOR UPDATE" : ""), siteId, tenantId))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        { if (!await r.ReadAsync(ct)) throw new StructureException("SITE_NOT_FOUND", "Parking site was not found in this tenant."); site = Profile(r); }
        var units = new List<SpatialUnit>(); var slots = new List<ParkingSlot>(); var paths = new List<AccessPath>();
        await using (var cmd = Command(db, tx, "SELECT id,parent_id,path,unit_type,name,max_capacity FROM spatial_units WHERE site_id=$1 AND tenant_id=$2 AND deleted_at IS NULL", siteId, tenantId))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct)) units.Add(new(r.GetGuid(0), r.IsDBNull(1) ? null : r.GetGuid(1), r.GetString(2), Enum.Parse<UnitType>(r.GetString(3)), r.GetString(4), r.GetInt32(5)));
        await using (var cmd = Command(db, tx, "SELECT id,spatial_unit_id,slot_code,supported_vehicle_type,slot_type,CASE physical_state WHEN 'AVAILABLE' THEN 'OPERATIONAL' WHEN 'OCCUPIED' THEN 'OPERATIONAL' WHEN 'UNAVAILABLE' THEN 'BLOCKED' ELSE physical_state END,(physical_state='OCCUPIED'),coordinates_3d::text,features::text,reservation_state FROM parking_slots WHERE site_id=$1 AND tenant_id=$2 AND deleted_at IS NULL" + (write ? " FOR UPDATE" : ""), siteId, tenantId))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct)) slots.Add(new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), Enum.Parse<VehicleType>(r.GetString(3)), Enum.Parse<SlotType>(r.GetString(4)),
                Enum.Parse<OperationalStatus>(r.GetString(5)), r.GetBoolean(6), r.IsDBNull(7) ? null : r.GetString(7), r.IsDBNull(8) ? null : r.GetString(8), r.IsDBNull(9) ? null : r.GetString(9)));
        await using (var cmd = Command(db, tx, "SELECT id,path_code,from_unit_id,to_unit_id,map_data::text FROM parking_access_paths WHERE site_id=$1 AND tenant_id=$2 AND deleted_at IS NULL", siteId, tenantId))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct)) paths.Add(new(r.GetGuid(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetGuid(2), r.IsDBNull(3) ? null : r.GetGuid(3), r.IsDBNull(4) ? null : r.GetString(4)));
        return new(site, units, slots, paths, snapshot?.ProtectedSlots, snapshot?.HasLiveCommitments ?? false);
    }
}
