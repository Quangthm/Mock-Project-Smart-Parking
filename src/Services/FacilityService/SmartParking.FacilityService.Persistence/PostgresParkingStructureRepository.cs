using Npgsql;
using SmartParking.Contracts.Facility;
using SmartParking.FacilityService.Application;
using SmartParking.FacilityService.Application.Common.Interfaces.Services;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Persistence;

// One transaction and site row lock per mutation. Register as scoped; NpgsqlDataSource is singleton.
public sealed class PostgresParkingStructureRepository(
    NpgsqlDataSource source,
    ParkingDbContext? context = null,
    ILayoutInvalidationPublisher? layoutInvalidations = null) : IParkingStructureRepository
{
    internal static NpgsqlCommand Command(NpgsqlConnection db, NpgsqlTransaction tx, string sql, params object?[] values)
    {
        var command = new NpgsqlCommand(sql, db, tx);
        foreach (var value in values) command.Parameters.Add(new NpgsqlParameter { Value = value ?? DBNull.Value });
        return command;
    }
    internal static async Task Execute(NpgsqlConnection db, NpgsqlTransaction tx, string sql, CancellationToken ct, params object?[] values)
    { await using var cmd = Command(db, tx, sql, values); await cmd.ExecuteNonQueryAsync(ct); }
    private static async Task EnsureTenantAsync(
        NpgsqlConnection db, NpgsqlTransaction tx, OwnerScope owner, CancellationToken ct)
    {
        await using var command = Command(db, tx,
            "SELECT id FROM tenants WHERE id=$1 AND status='ACTIVE' AND deleted_at IS NULL FOR SHARE",
            owner.TenantId);
        if (await command.ExecuteScalarAsync(ct) is null)
        {
            throw new StructureException("FORBIDDEN", "Active tenant is required.");
        }
    }
    private static SiteProfile Profile(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetString(4),
        r.IsDBNull(5) ? null : r.GetDecimal(5), r.IsDBNull(6) ? null : r.GetDecimal(6), r.GetString(7) == "ACTIVE", r.GetInt32(8), r.GetString(7));
    private const string SiteColumns = "id, tenant_id, site_code, name, address, latitude, longitude, status, total_physical_capacity";
    internal static string AuditState(ParkingStructure structure) => System.Text.Json.JsonSerializer.Serialize(new { structure.Site, structure.Units, structure.Slots, structure.Paths, structure.BackupPolicies });

    public async Task<IReadOnlyList<SiteProfile>> ListAsync(OwnerScope owner, CancellationToken ct)
    {
        await using var db = await source.OpenConnectionAsync(ct); await using var tx = await db.BeginTransactionAsync(ct);
        await EnsureTenantAsync(db, tx, owner, ct);
        var result = new List<SiteProfile>();
        await using (var cmd = Command(db, tx, $"SELECT {SiteColumns} FROM parking_sites WHERE tenant_id = $1 AND deleted_at IS NULL ORDER BY site_code", owner.TenantId))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct)) result.Add(Profile(reader));
        await tx.CommitAsync(ct); return result;
    }
    public async Task<SiteProfile> CreateAsync(OwnerScope owner, SiteProfile site, CancellationToken ct)
    {
        if (site.TenantId != owner.TenantId)
            throw new StructureException("FORBIDDEN", "Site must belong to the supplied tenant.");
        await using var txScope = await FacilityOutboxTransaction.BeginAsync(source, context, ct);
        var db = txScope.Connection;
        var tx = txScope.Transaction;
        await EnsureTenantAsync(db, tx, owner, ct);
        try
        {
            await Execute(db, tx, """
                INSERT INTO parking_sites(id, tenant_id, site_code, name, address, latitude, longitude)
                VALUES ($1, $2, $3, $4, $5, $6::numeric, $7::numeric)
                """, ct, site.Id, site.TenantId, site.Code, site.Name, site.Address, site.Latitude, site.Longitude);
            await Execute(db, tx, "INSERT INTO structure_change_audit(id,site_id,actor_id,after_state) VALUES ($1,$2,$3,$4::jsonb)", ct, Guid.NewGuid(), site.Id, owner.UserId, AuditState(new ParkingStructure(site)));
            await txScope.StageAndCommitAsync(
                layoutInvalidations,
                site.TenantId,
                site.Id,
                [LayoutInvalidationResources.Structure, LayoutInvalidationResources.FullLayout, LayoutInvalidationResources.CapacitySummary],
                ct);
            return site;
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
        { throw new StructureException("DUPLICATE_IDENTIFIER", "Site code already exists in this tenant."); }
    }

    public async Task<ParkingStructure> ReadAsync(
        OwnerScope owner, Guid siteId, CommitmentSnapshot? snapshot, CancellationToken ct)
    {
        await using var db = await source.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        await EnsureTenantAsync(db, tx, owner, ct);
        var structure = await Load(db, tx, owner.TenantId, siteId, false, ct, snapshot);
        await tx.CommitAsync(ct);
        return structure;
    }

    public async Task<IParkingStructureEdit> BeginEditAsync(
        OwnerScope owner, Guid siteId, CommitmentSnapshot snapshot, Guid? operationId, CancellationToken ct)
    {
        var txScope = await FacilityOutboxTransaction.BeginAsync(source, context, ct);
        try
        {
            var db = txScope.Connection;
            var tx = txScope.Transaction;
            await EnsureTenantAsync(db, tx, owner, ct);

            if (operationId is { } operation)
            {
                await using var check = Command(db, tx,
                    "SELECT physical_committed FROM structure_operations WHERE id=$1 AND site_id=$2 AND tenant_id=$3 AND actor_id=$4 FOR UPDATE",
                    operation, siteId, owner.TenantId, owner.UserId);
                var committed = await check.ExecuteScalarAsync(ct);
                if (committed is null)
                    throw new StructureException("FORBIDDEN", "Operation is not in scope.");
                if ((bool)committed)
                    throw new StructureException("STRUCTURE_CONFLICT", "This physical operation has already committed.");
            }

            var structure = await Load(db, tx, owner.TenantId, siteId, true, ct, snapshot);
            return new ParkingStructureEdit(txScope, owner, siteId, operationId, structure, layoutInvalidations);
        }
        catch
        {
            await txScope.DisposeAsync();
            throw;
        }
    }

    private static async Task<ParkingStructure> Load(NpgsqlConnection db, NpgsqlTransaction tx, Guid tenantId, Guid siteId, bool write, CancellationToken ct, CommitmentSnapshot? snapshot = null)
    {
        SiteProfile site; DateTime siteUpdatedAt;
        await using (var cmd = Command(db, tx, $"SELECT {SiteColumns}, updated_at FROM parking_sites WHERE id=$1 AND tenant_id=$2 AND deleted_at IS NULL" + (write ? " FOR UPDATE" : ""), siteId, tenantId))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        { if (!await r.ReadAsync(ct)) throw new StructureException("SITE_NOT_FOUND", "Parking site was not found in this tenant."); site = Profile(r); siteUpdatedAt = r.GetDateTime(9); }
        // Version tokens for commit-time expectedUpdatedAt rechecks in a batch.
        var versions = new Dictionary<Guid, DateTime> { [site.Id] = siteUpdatedAt };
        var units = new List<SpatialUnit>(); var slots = new List<ParkingSlot>(); var paths = new List<AccessPath>();
        var releasedUnits = new List<SpatialUnit>(); var releasedSlots = new List<ParkingSlot>(); var releasedPaths = new List<AccessPath>();
        await using (var cmd = Command(db, tx, "SELECT id,parent_id,path,unit_type,name,max_capacity,updated_at FROM spatial_units WHERE site_id=$1 AND tenant_id=$2 AND deleted_at IS NULL", siteId, tenantId))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct))
            {
                units.Add(new(r.GetGuid(0), r.IsDBNull(1) ? null : r.GetGuid(1), r.GetString(2), Enum.Parse<UnitType>(r.GetString(3)), r.GetString(4), r.GetInt32(5)));
                versions[r.GetGuid(0)] = r.GetDateTime(6);
            }
        await using (var cmd = Command(db, tx, "SELECT id,parent_id,path,unit_type,name,max_capacity FROM spatial_units WHERE site_id=$1 AND tenant_id=$2 AND deleted_at IS NOT NULL", siteId, tenantId))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct)) releasedUnits.Add(new(r.GetGuid(0), r.IsDBNull(1) ? null : r.GetGuid(1), r.GetString(2), Enum.Parse<UnitType>(r.GetString(3)), r.GetString(4), r.GetInt32(5)));
        await using (var cmd = Command(db, tx, "SELECT id,spatial_unit_id,slot_code,supported_vehicle_type,slot_type,CASE physical_state WHEN 'AVAILABLE' THEN 'OPERATIONAL' WHEN 'OCCUPIED' THEN 'OPERATIONAL' WHEN 'UNAVAILABLE' THEN 'BLOCKED' ELSE physical_state END,(physical_state='OCCUPIED'),coordinates_3d::text,features::text,reservation_state,updated_at FROM parking_slots WHERE site_id=$1 AND tenant_id=$2 AND deleted_at IS NULL" + (write ? " FOR UPDATE" : ""), siteId, tenantId))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct))
            {
                var vType = r.IsDBNull(3) ? VehicleType.CAR : Enum.Parse<VehicleType>(r.GetString(3)); var sType = r.IsDBNull(4) ? SlotType.STANDARD : Enum.Parse<SlotType>(r.GetString(4)); var opStatus = r.IsDBNull(5) ? OperationalStatus.OPERATIONAL : Enum.Parse<OperationalStatus>(r.GetString(5)); slots.Add(new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), vType, sType, opStatus, r.GetBoolean(6), r.IsDBNull(7) ? null : r.GetString(7), r.IsDBNull(8) ? null : r.GetString(8), r.IsDBNull(9) ? null : r.GetString(9)));
                versions[r.GetGuid(0)] = r.GetDateTime(10);
            }
        // Released rows are read-only candidates for an explicit restore. A NULL projection column cannot
        // be represented as a ParkingSlot (DE-6), so such a row is not offered for restoration.
        await using (var cmd = Command(db, tx, "SELECT id,spatial_unit_id,slot_code,supported_vehicle_type,slot_type,CASE physical_state WHEN 'AVAILABLE' THEN 'OPERATIONAL' WHEN 'OCCUPIED' THEN 'OPERATIONAL' WHEN 'UNAVAILABLE' THEN 'BLOCKED' ELSE physical_state END,(physical_state='OCCUPIED'),coordinates_3d::text,features::text,reservation_state FROM parking_slots WHERE site_id=$1 AND tenant_id=$2 AND deleted_at IS NOT NULL AND supported_vehicle_type IS NOT NULL", siteId, tenantId))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct)) { var rvType = r.IsDBNull(3) ? VehicleType.CAR : Enum.Parse<VehicleType>(r.GetString(3)); var rsType = r.IsDBNull(4) ? SlotType.STANDARD : Enum.Parse<SlotType>(r.GetString(4)); var ropStatus = r.IsDBNull(5) ? OperationalStatus.OPERATIONAL : Enum.Parse<OperationalStatus>(r.GetString(5)); releasedSlots.Add(new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), rvType, rsType, ropStatus, r.GetBoolean(6), r.IsDBNull(7) ? null : r.GetString(7), r.IsDBNull(8) ? null : r.GetString(8), r.IsDBNull(9) ? null : r.GetString(9))); }
        // The table is the source of truth for eligibility. One query per load, covering released rows so a
        // resurrection reuses the surviving compatibility set; with no rows the projection column stands in.
        var compatibility = new Dictionary<Guid, VehicleType[]>();
        if (slots.Count + releasedSlots.Count > 0)
        {
            var rows = new List<(Guid SlotId, VehicleType Type)>();
            await using (var cmd = Command(db, tx, "SELECT slot_id,vehicle_type FROM slot_compatibilities WHERE site_id=$1 AND tenant_id=$2 AND slot_id = ANY($3::uuid[])", siteId, tenantId, slots.Concat(releasedSlots).Select(slot => slot.Id).ToArray()))
            await using (var r = await cmd.ExecuteReaderAsync(ct))
                while (await r.ReadAsync(ct)) { var cType = r.IsDBNull(1) ? VehicleType.CAR : Enum.Parse<VehicleType>(r.GetString(1)); rows.Add((r.GetGuid(0), cType)); }
            foreach (var group in rows.GroupBy(row => row.SlotId))
                compatibility[group.Key] = group.Select(row => row.Type).ToArray();
        }
        for (var index = 0; index < slots.Count; index++)
        {
            VehicleType[] declared = compatibility.TryGetValue(slots[index].Id, out var values) ? values : [slots[index].VehicleType]; var primaryType = declared.Length > 0 ? declared[0] : slots[index].VehicleType; slots[index] = slots[index] with { VehicleType = primaryType, VehicleTypes = declared };
        }
        for (var index = 0; index < releasedSlots.Count; index++)
        {
            VehicleType[] declared = compatibility.TryGetValue(releasedSlots[index].Id, out var values) ? values : [releasedSlots[index].VehicleType]; var primaryType = declared.Length > 0 ? declared[0] : releasedSlots[index].VehicleType; releasedSlots[index] = releasedSlots[index] with { VehicleType = primaryType, VehicleTypes = declared };
        }
        await using (var cmd = Command(db, tx, "SELECT id,path_code,from_unit_id,to_unit_id,map_data::text,created_at FROM parking_access_paths WHERE site_id=$1 AND tenant_id=$2 AND deleted_at IS NULL", siteId, tenantId))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct))
            {
                paths.Add(new(r.GetGuid(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetGuid(2), r.IsDBNull(3) ? null : r.GetGuid(3), r.IsDBNull(4) ? null : r.GetString(4)));
                // Paths have no updated_at column: an immutable row's created_at is its version.
                versions[r.GetGuid(0)] = r.GetDateTime(5);
            }
        await using (var cmd = Command(db, tx, "SELECT id,path_code,from_unit_id,to_unit_id,map_data::text FROM parking_access_paths WHERE site_id=$1 AND tenant_id=$2 AND deleted_at IS NOT NULL", siteId, tenantId))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct)) releasedPaths.Add(new(r.GetGuid(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetGuid(2), r.IsDBNull(3) ? null : r.GetGuid(3), r.IsDBNull(4) ? null : r.GetString(4)));
        var backups = new List<BackupPolicy>();
        await using (var cmd = Command(db, tx, "SELECT unit_id,vehicle_type,count FROM structure_backup_policies WHERE site_id=$1", siteId))
        await using (var r = await cmd.ExecuteReaderAsync(ct)) while (await r.ReadAsync(ct)) backups.Add(new(r.IsDBNull(0) ? null : r.GetGuid(0), Enum.Parse<VehicleType>(r.GetString(1)), r.GetInt32(2)));
        return new(site, units, slots, paths, snapshot?.ProtectedSlots, snapshot?.HasLiveCommitments ?? false, backups, snapshot?.Capacities,
            releasedSlots, releasedUnits, releasedPaths, versions);
    }
}
