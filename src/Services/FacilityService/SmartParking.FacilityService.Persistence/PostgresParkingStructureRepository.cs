using Npgsql;
using SmartParking.FacilityService.Application;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Persistence;

// One transaction and site row lock per mutation. Register as scoped; NpgsqlDataSource is singleton.
public sealed class PostgresParkingStructureRepository(NpgsqlDataSource source) : IParkingStructureRepository
{
    private static NpgsqlCommand Command(NpgsqlConnection db, NpgsqlTransaction tx, string sql, params object?[] values)
    {
        var command = new NpgsqlCommand(sql, db, tx);
        foreach (var value in values) command.Parameters.Add(new NpgsqlParameter { Value = value ?? DBNull.Value });
        return command;
    }
    private static async Task Execute(NpgsqlConnection db, NpgsqlTransaction tx, string sql, CancellationToken ct, params object?[] values)
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
    private static string AuditState(ParkingStructure structure) => System.Text.Json.JsonSerializer.Serialize(new { structure.Site, structure.Units, structure.Slots, structure.Paths, structure.BackupPolicies });

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
        await using var db = await source.OpenConnectionAsync(ct); await using var tx = await db.BeginTransactionAsync(ct);
        await EnsureTenantAsync(db, tx, owner, ct);
        try
        {
            await Execute(db, tx, """
                INSERT INTO parking_sites(id, tenant_id, site_code, name, address, latitude, longitude)
                VALUES ($1, $2, $3, $4, $5, $6::numeric, $7::numeric)
                """, ct, site.Id, site.TenantId, site.Code, site.Name, site.Address, site.Latitude, site.Longitude);
            await Execute(db, tx, "INSERT INTO structure_change_audit(id,site_id,actor_id,after_state) VALUES ($1,$2,$3,$4::jsonb)", ct, Guid.NewGuid(), site.Id, owner.UserId, AuditState(new ParkingStructure(site)));
            await tx.CommitAsync(ct); return site;
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
        { throw new StructureException("DUPLICATE_IDENTIFIER", "Site code already exists in this tenant."); }
    }

    public async Task<ParkingStructure> ReadAsync(
        OwnerScope owner, Guid siteId, CommitmentSnapshot snapshot, CancellationToken ct)
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
        var db = await source.OpenConnectionAsync(ct);
        NpgsqlTransaction? tx = null;
        try
        {
            tx = await db.BeginTransactionAsync(ct);
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
            return new ParkingStructureEdit(db, tx, owner, siteId, operationId, structure);
        }
        catch
        {
            if (tx is not null) await tx.DisposeAsync();
            await db.DisposeAsync();
            throw;
        }
    }

    private sealed class ParkingStructureEdit(
        NpgsqlConnection db, NpgsqlTransaction tx, OwnerScope owner,
        Guid siteId, Guid? operationId, ParkingStructure structure) : IParkingStructureEdit
    {
        private readonly HashSet<Guid> liveUnitsAtLoad = structure.Units.Select(unit => unit.Id).ToHashSet();
        private readonly HashSet<Guid> liveSlotsAtLoad = structure.Slots.Select(slot => slot.Id).ToHashSet();
        private readonly HashSet<Guid> livePathsAtLoad = structure.Paths.Select(path => path.Id).ToHashSet();
        // A restored row already exists in the database, so it must take the UPDATE path and never be
        // INSERTed. The released rows are part of the "existing" set for exactly that reason.
        private readonly HashSet<Guid> existingUnits = structure.Units.Select(unit => unit.Id)
            .Union(structure.ReleasedUnits.Select(unit => unit.Id)).ToHashSet();
        private readonly HashSet<Guid> existingSlots = structure.Slots.Select(slot => slot.Id)
            .Union(structure.ReleasedSlots.Select(slot => slot.Id)).ToHashSet();
        private readonly HashSet<Guid> existingPaths = structure.Paths.Select(path => path.Id)
            .Union(structure.ReleasedPaths.Select(path => path.Id)).ToHashSet();
        private readonly string before = AuditState(structure);
        public ParkingStructure Structure => structure;

        public async Task CommitAsync(
            Guid synchronizationToken, StructureOutcome outcome,
            Guid? markedSlot, string? reason, CancellationToken ct)
        {
            try
            {
                var site = structure.Site;
                await Execute(db, tx, """
                    UPDATE parking_sites SET site_code=$2, name=$3, address=$4, latitude=$5::numeric, longitude=$6::numeric,
                    status=$7, total_physical_capacity=$8, updated_at=CURRENT_TIMESTAMP WHERE id=$1
                    """, ct, site.Id, site.Code, site.Name, site.Address, site.Latitude, site.Longitude, site.Status, structure.Slots.Count);
                // Children are soft-deleted before their parents. No historical reference is cascaded away.
                // Only rows that were live at load can become soft-deleted; released rows keep their original timestamp.
                foreach (var id in livePathsAtLoad.Except(structure.Paths.Select(p => p.Id)))
                    await Execute(db, tx, "UPDATE parking_access_paths SET deleted_at=CURRENT_TIMESTAMP WHERE id=$1", ct, id);
                foreach (var id in liveSlotsAtLoad.Except(structure.Slots.Select(s => s.Id)))
                    await Execute(db, tx, "UPDATE parking_slots SET deleted_at=CURRENT_TIMESTAMP WHERE id=$1", ct, id);
                foreach (var id in liveUnitsAtLoad.Except(structure.Units.Select(u => u.Id)))
                    await Execute(db, tx, "UPDATE spatial_units SET deleted_at=CURRENT_TIMESTAMP WHERE id=$1", ct, id);
                foreach (var u in structure.Units.OrderBy(u => u.Path.Length))
                {
                    if (existingUnits.Contains(u.Id))
                        await Execute(db, tx, "UPDATE spatial_units SET name=$2, max_capacity=$3, deleted_at=NULL, updated_at=CURRENT_TIMESTAMP WHERE id=$1", ct, u.Id, u.Name, u.MaxCapacity);
                    else
                    {
                        if (await ReleasedUnitAsync(u, ct) is { } released) throw released;
                        await Execute(db, tx, """
                            INSERT INTO spatial_units(id,tenant_id,site_id,parent_id,path,unit_type,name,max_capacity)
                            VALUES ($1,$2,$3,$4::uuid,$5,$6,$7,$8)
                            """, ct, u.Id, site.TenantId, site.Id, u.ParentId, u.Path, u.Type.ToString(), u.Name, u.MaxCapacity);
                    }
                }
                foreach (var s in structure.Slots)
                {
                    if (existingSlots.Contains(s.Id))
                    {
                        await Execute(db, tx, """
                            UPDATE parking_slots SET spatial_unit_id=$2, slot_code=$3, supported_vehicle_type=$4, slot_type=$5,
                            coordinates_3d=$6::jsonb, features=$7::jsonb, reservation_state=$8, deleted_at=NULL,
                            updated_at=CURRENT_TIMESTAMP WHERE id=$1
                            """, ct, s.Id, s.UnitId, s.Code, s.VehicleType.ToString(), s.Type.ToString(), s.Coordinates3D, s.Features, s.ReservationState);
                        // Resurrection returns the slot to service. Deletion only allowed a non-occupied row,
                        // so the stored physical state is cleared instead of resurrecting a stale overlay.
                        if (!liveSlotsAtLoad.Contains(s.Id))
                            await Execute(db, tx, "UPDATE parking_slots SET physical_state='AVAILABLE', reservation_state=NULL, deleted_at=NULL, updated_at=CURRENT_TIMESTAMP WHERE id=$1", ct, s.Id);
                    }
                    else
                    {
                        if (await ReleasedSlotAsync(s, ct) is { } released) throw released;
                        await Execute(db, tx, """
                            INSERT INTO parking_slots(id,tenant_id,site_id,spatial_unit_id,slot_code,supported_vehicle_type,slot_type,coordinates_3d,features)
                            VALUES ($1,$2,$3,$4,$5,$6,$7,$8::jsonb,$9::jsonb)
                            """, ct, s.Id, site.TenantId, site.Id, s.UnitId, s.Code, s.VehicleType.ToString(), s.Type.ToString(), s.Coordinates3D, s.Features);
                    }
                }
                // Compatibility is the source of truth and lives in the same transaction as the slot row.
                // Surplus rows are deleted so the table matches the declared set exactly.
                foreach (var s in structure.Slots)
                {
                    await Execute(db, tx, "DELETE FROM slot_compatibilities WHERE slot_id=$1", ct, s.Id);
                    IReadOnlyList<VehicleType> compatibility = s.VehicleTypes.Count > 0 ? s.VehicleTypes : [s.VehicleType];
                    foreach (var vehicleType in compatibility.Distinct())
                        await Execute(db, tx, """
                            INSERT INTO slot_compatibilities(tenant_id,site_id,slot_id,vehicle_type)
                            VALUES ($1,$2,$3,$4)
                            """, ct, site.TenantId, site.Id, s.Id, vehicleType.ToString());
                }
                foreach (var p in structure.Paths)
                {
                    if (existingPaths.Contains(p.Id))
                    {
                        // Only a restored path needs writing; a live path is never modified by this pipeline.
                        if (!livePathsAtLoad.Contains(p.Id))
                            await Execute(db, tx, """
                                UPDATE parking_access_paths SET path_code=$2, from_unit_id=$3::uuid, to_unit_id=$4::uuid,
                                map_data=$5::jsonb, deleted_at=NULL WHERE id=$1
                                """, ct, p.Id, p.Code, p.FromUnitId, p.ToUnitId, p.MapData);
                    }
                    else
                    {
                        if (await ReleasedPathAsync(p, ct) is { } released) throw released;
                        await Execute(db, tx, """
                            INSERT INTO parking_access_paths(id,tenant_id,site_id,path_code,from_unit_id,to_unit_id,map_data)
                            VALUES ($1,$2,$3,$4,$5::uuid,$6::uuid,$7::jsonb)
                            """, ct, p.Id, site.TenantId, site.Id, p.Code, p.FromUnitId, p.ToUnitId, p.MapData);
                    }
                }
                await Execute(db, tx, "DELETE FROM structure_backup_policies WHERE site_id=$1", ct, siteId);
                foreach (var policy in structure.BackupPolicies)
                    await Execute(db, tx, "INSERT INTO structure_backup_policies(id,site_id,unit_id,vehicle_type,count) VALUES ($1,$2,$3,$4,$5)", ct, Guid.NewGuid(), siteId, policy.UnitId, policy.VehicleType.ToString(), policy.Count);
                if (operationId is { } applied) await Execute(db, tx, "UPDATE structure_operations SET physical_committed=TRUE,updated_at=now() WHERE id=$1", ct, applied);
                await Execute(db, tx, "INSERT INTO structure_change_audit(id,site_id,actor_id,operation_id,before_state,after_state) VALUES ($1,$2,$3,$4::uuid,$5::jsonb,$6::jsonb)", ct, Guid.NewGuid(), siteId, owner.UserId, operationId, before, AuditState(structure));
                if (markedSlot is { } marked)
                {
                    await Execute(db, tx, "INSERT INTO structure_slot_audit(id,site_id,slot_id,actor_id,action,reason) VALUES ($1,$2,$3,$4,$5,$6)", ct, Guid.NewGuid(), siteId, marked, owner.UserId, structure.Slots.Single(slot => slot.Id == marked).ReservationState == "BACKUP" ? "MARK_BACKUP" : "RELEASE_BACKUP", reason);
                }

                await using var ready = Command(db, tx, """
                    UPDATE structure_synchronizations SET state='ready',outcome=$2::jsonb,updated_at=now()
                    WHERE token=$1 AND state='preparing' AND site_id=$3 AND tenant_id=$4
                    """, synchronizationToken,
                    System.Text.Json.JsonSerializer.Serialize(outcome, StructureSynchronizationJson.Options),
                    siteId, owner.TenantId);
                if (await ready.ExecuteNonQueryAsync(ct) != 1)
                    throw new StructureException("STRUCTURE_CONFLICT", "Synchronization preparation is missing.");

                await tx.CommitAsync(ct);
            }
            catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                throw new StructureException("DUPLICATE_IDENTIFIER", "Structure identifier already exists.");
            }
        }

        // A create never resurrects a released row: it is checked in the same transaction as the INSERT,
        // so naming a released identifier writes nothing and asks the Owner to restore it explicitly.
        private async Task<ReleasedIdentifierException?> ReleasedSlotAsync(ParkingSlot slot, CancellationToken ct)
        {
            await using var cmd = Command(db, tx, """
                SELECT id, slot_code, deleted_at FROM parking_slots
                WHERE tenant_id=$1 AND site_id=$2 AND deleted_at IS NOT NULL AND upper(slot_code)=upper($3)
                LIMIT 1
                """, structure.Site.TenantId, siteId, slot.Code);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return null;
            return new(r.GetGuid(0), r.GetString(1), r.GetDateTime(2),
                "This slot code belongs to a released slot; restore it explicitly.");
        }
        private async Task<ReleasedIdentifierException?> ReleasedUnitAsync(SpatialUnit unit, CancellationToken ct)
        {
            await using var cmd = Command(db, tx, """
                SELECT id, name, deleted_at FROM spatial_units
                WHERE tenant_id=$1 AND site_id=$2 AND deleted_at IS NOT NULL
                  AND parent_id IS NOT DISTINCT FROM $3::uuid
                  AND lower(name)=lower($4) AND unit_type=$5
                LIMIT 1
                """, structure.Site.TenantId, siteId, unit.ParentId, unit.Name, unit.Type.ToString());
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return null;
            return new(r.GetGuid(0), r.GetString(1), r.GetDateTime(2),
                "This unit name belongs to a released unit; restore it explicitly.");
        }
        private async Task<ReleasedIdentifierException?> ReleasedPathAsync(AccessPath path, CancellationToken ct)
        {
            await using var cmd = Command(db, tx, """
                SELECT id, path_code, deleted_at FROM parking_access_paths
                WHERE tenant_id=$1 AND site_id=$2 AND deleted_at IS NOT NULL AND upper(path_code)=upper($3)
                LIMIT 1
                """, structure.Site.TenantId, siteId, path.Code);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return null;
            return new(r.GetGuid(0), r.GetString(1), r.GetDateTime(2),
                "This path code belongs to a released path; restore it explicitly.");
        }

        public async ValueTask DisposeAsync()
        {
            try { await tx.DisposeAsync(); }
            finally { await db.DisposeAsync(); }
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
                slots.Add(new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), Enum.Parse<VehicleType>(r.GetString(3)), Enum.Parse<SlotType>(r.GetString(4)),
                    Enum.Parse<OperationalStatus>(r.GetString(5)), r.GetBoolean(6), r.IsDBNull(7) ? null : r.GetString(7), r.IsDBNull(8) ? null : r.GetString(8), r.IsDBNull(9) ? null : r.GetString(9)));
                versions[r.GetGuid(0)] = r.GetDateTime(10);
            }
        // Released rows are read-only candidates for an explicit restore. A NULL projection column cannot
        // be represented as a ParkingSlot (DE-6), so such a row is not offered for restoration.
        await using (var cmd = Command(db, tx, "SELECT id,spatial_unit_id,slot_code,supported_vehicle_type,slot_type,CASE physical_state WHEN 'AVAILABLE' THEN 'OPERATIONAL' WHEN 'OCCUPIED' THEN 'OPERATIONAL' WHEN 'UNAVAILABLE' THEN 'BLOCKED' ELSE physical_state END,(physical_state='OCCUPIED'),coordinates_3d::text,features::text,reservation_state FROM parking_slots WHERE site_id=$1 AND tenant_id=$2 AND deleted_at IS NOT NULL AND supported_vehicle_type IS NOT NULL", siteId, tenantId))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct)) releasedSlots.Add(new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), Enum.Parse<VehicleType>(r.GetString(3)), Enum.Parse<SlotType>(r.GetString(4)),
                Enum.Parse<OperationalStatus>(r.GetString(5)), r.GetBoolean(6), r.IsDBNull(7) ? null : r.GetString(7), r.IsDBNull(8) ? null : r.GetString(8), r.IsDBNull(9) ? null : r.GetString(9)));
        // The table is the source of truth for eligibility. One query per load, covering released rows so a
        // resurrection reuses the surviving compatibility set; with no rows the projection column stands in.
        var compatibility = new Dictionary<Guid, VehicleType[]>();
        if (slots.Count + releasedSlots.Count > 0)
        {
            var rows = new List<(Guid SlotId, VehicleType Type)>();
            await using (var cmd = Command(db, tx, "SELECT slot_id,vehicle_type FROM slot_compatibilities WHERE site_id=$1 AND tenant_id=$2 AND slot_id = ANY($3::uuid[])", siteId, tenantId, slots.Concat(releasedSlots).Select(slot => slot.Id).ToArray()))
            await using (var r = await cmd.ExecuteReaderAsync(ct))
                while (await r.ReadAsync(ct)) rows.Add((r.GetGuid(0), Enum.Parse<VehicleType>(r.GetString(1))));
            foreach (var group in rows.GroupBy(row => row.SlotId))
                compatibility[group.Key] = group.Select(row => row.Type).ToArray();
        }
        for (var index = 0; index < slots.Count; index++)
        {
            VehicleType[] declared = compatibility.TryGetValue(slots[index].Id, out var values) ? values : [slots[index].VehicleType];
            slots[index] = slots[index] with { VehicleTypes = declared };
        }
        for (var index = 0; index < releasedSlots.Count; index++)
        {
            VehicleType[] declared = compatibility.TryGetValue(releasedSlots[index].Id, out var values) ? values : [releasedSlots[index].VehicleType];
            releasedSlots[index] = releasedSlots[index] with { VehicleTypes = declared };
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
