using Npgsql;
using SmartParking.Contracts.Facility;
using SmartParking.FacilityService.Application;
using SmartParking.FacilityService.Application.Common.Interfaces.Services;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Persistence;

internal sealed class ParkingStructureEdit(
    FacilityOutboxTransaction txScope,
    OwnerScope owner,
    Guid siteId,
    Guid? operationId,
    ParkingStructure structure,
    ILayoutInvalidationPublisher? layoutInvalidations) : IParkingStructureEdit
{
    private readonly NpgsqlConnection db = txScope.Connection;
    private readonly NpgsqlTransaction tx = txScope.Transaction;
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
    private readonly string before = PostgresParkingStructureRepository.AuditState(structure);
    public ParkingStructure Structure => structure;

    public async Task CommitAsync(
        Guid synchronizationToken,
        StructureOutcome outcome,
        Guid? markedSlot,
        string? reason,
        CancellationToken ct)
    {
        try
        {
            var site = structure.Site;
            await PostgresParkingStructureRepository.Execute(db, tx, """
                UPDATE parking_sites SET site_code=$2, name=$3, address=$4, latitude=$5::numeric, longitude=$6::numeric,
                status=$7, total_physical_capacity=$8, updated_at=CURRENT_TIMESTAMP WHERE id=$1
                """, ct, site.Id, site.Code, site.Name, site.Address, site.Latitude, site.Longitude, site.Status, structure.Slots.Count);
            // Children are soft-deleted before their parents. No historical reference is cascaded away.
            // Only rows that were live at load can become soft-deleted; released rows keep their original timestamp.
            foreach (var id in livePathsAtLoad.Except(structure.Paths.Select(p => p.Id)))
                await PostgresParkingStructureRepository.Execute(db, tx, "UPDATE parking_access_paths SET deleted_at=CURRENT_TIMESTAMP WHERE id=$1", ct, id);
            foreach (var id in liveSlotsAtLoad.Except(structure.Slots.Select(s => s.Id)))
                await PostgresParkingStructureRepository.Execute(db, tx, "UPDATE parking_slots SET deleted_at=CURRENT_TIMESTAMP WHERE id=$1", ct, id);
            foreach (var id in liveUnitsAtLoad.Except(structure.Units.Select(u => u.Id)))
                await PostgresParkingStructureRepository.Execute(db, tx, "UPDATE spatial_units SET deleted_at=CURRENT_TIMESTAMP WHERE id=$1", ct, id);
            foreach (var u in structure.Units.OrderBy(u => u.Path.Length))
            {
                if (existingUnits.Contains(u.Id))
                    await PostgresParkingStructureRepository.Execute(db, tx, "UPDATE spatial_units SET name=$2, max_capacity=$3, deleted_at=NULL, updated_at=CURRENT_TIMESTAMP WHERE id=$1", ct, u.Id, u.Name, u.MaxCapacity);
                else
                {
                    if (await ReleasedUnitAsync(u, ct) is { } released) throw released;
                    await PostgresParkingStructureRepository.Execute(db, tx, """
                        INSERT INTO spatial_units(id,tenant_id,site_id,parent_id,path,unit_type,name,max_capacity)
                        VALUES ($1,$2,$3,$4::uuid,$5,$6,$7,$8)
                        """, ct, u.Id, site.TenantId, site.Id, u.ParentId, u.Path, u.Type.ToString(), u.Name, u.MaxCapacity);
                }
            }
            foreach (var s in structure.Slots)
            {
                if (existingSlots.Contains(s.Id))
                {
                    await PostgresParkingStructureRepository.Execute(db, tx, """
                        UPDATE parking_slots SET spatial_unit_id=$2, slot_code=$3, supported_vehicle_type=$4, slot_type=$5,
                        coordinates_3d=$6::jsonb, features=$7::jsonb, reservation_state=$8, deleted_at=NULL,
                        updated_at=CURRENT_TIMESTAMP WHERE id=$1
                        """, ct, s.Id, s.UnitId, s.Code, s.VehicleType.ToString(), s.Type.ToString(), s.Coordinates3D, s.Features, s.ReservationState);
                    // Resurrection returns the slot to service. Deletion only allowed a non-occupied row,
                    // so the stored physical state is cleared instead of resurrecting a stale overlay.
                    if (!liveSlotsAtLoad.Contains(s.Id))
                        await PostgresParkingStructureRepository.Execute(db, tx, "UPDATE parking_slots SET physical_state='AVAILABLE', reservation_state=NULL, deleted_at=NULL, updated_at=CURRENT_TIMESTAMP WHERE id=$1", ct, s.Id);
                }
                else
                {
                    if (await ReleasedSlotAsync(s, ct) is { } released) throw released;
                    await PostgresParkingStructureRepository.Execute(db, tx, """
                        INSERT INTO parking_slots(id,tenant_id,site_id,spatial_unit_id,slot_code,supported_vehicle_type,slot_type,coordinates_3d,features)
                        VALUES ($1,$2,$3,$4,$5,$6,$7,$8::jsonb,$9::jsonb)
                        """, ct, s.Id, site.TenantId, site.Id, s.UnitId, s.Code, s.VehicleType.ToString(), s.Type.ToString(), s.Coordinates3D, s.Features);
                }
            }
            // Compatibility is the source of truth and lives in the same transaction as the slot row.
            // Surplus rows are deleted so the table matches the declared set exactly.
            foreach (var s in structure.Slots)
            {
                await PostgresParkingStructureRepository.Execute(db, tx, "DELETE FROM slot_compatibilities WHERE slot_id=$1", ct, s.Id);
                IReadOnlyList<VehicleType> compatibility = s.VehicleTypes.Count > 0 ? s.VehicleTypes : [s.VehicleType];
                foreach (var vehicleType in compatibility.Distinct())
                    await PostgresParkingStructureRepository.Execute(db, tx, """
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
                        await PostgresParkingStructureRepository.Execute(db, tx, """
                            UPDATE parking_access_paths SET path_code=$2, from_unit_id=$3::uuid, to_unit_id=$4::uuid,
                            map_data=$5::jsonb, deleted_at=NULL WHERE id=$1
                            """, ct, p.Id, p.Code, p.FromUnitId, p.ToUnitId, p.MapData);
                }
                else
                {
                    if (await ReleasedPathAsync(p, ct) is { } released) throw released;
                    await PostgresParkingStructureRepository.Execute(db, tx, """
                        INSERT INTO parking_access_paths(id,tenant_id,site_id,path_code,from_unit_id,to_unit_id,map_data)
                        VALUES ($1,$2,$3,$4,$5::uuid,$6::uuid,$7::jsonb)
                        """, ct, p.Id, site.TenantId, site.Id, p.Code, p.FromUnitId, p.ToUnitId, p.MapData);
                }
            }
            await PostgresParkingStructureRepository.Execute(db, tx, "DELETE FROM structure_backup_policies WHERE site_id=$1", ct, siteId);
            foreach (var policy in structure.BackupPolicies)
                await PostgresParkingStructureRepository.Execute(db, tx, "INSERT INTO structure_backup_policies(id,tenant_id,site_id,unit_id,vehicle_type,count) VALUES ($1,$2,$3,$4,$5,$6)", ct, Guid.NewGuid(), site.TenantId, siteId, policy.UnitId, policy.VehicleType.ToString(), policy.Count);
            if (operationId is { } applied) await PostgresParkingStructureRepository.Execute(db, tx, "UPDATE structure_operations SET physical_committed=TRUE,updated_at=now() WHERE id=$1", ct, applied);
            var after = PostgresParkingStructureRepository.AuditState(structure);
            var layoutChanged = !string.Equals(before, after, StringComparison.Ordinal) ||
                structure.Site.TotalPhysicalCapacity != structure.Slots.Count;
            await PostgresParkingStructureRepository.Execute(db, tx, "INSERT INTO structure_change_audit(id,site_id,actor_id,operation_id,before_state,after_state) VALUES ($1,$2,$3,$4::uuid,$5::jsonb,$6::jsonb)", ct, Guid.NewGuid(), siteId, owner.UserId, operationId, before, after);
            if (markedSlot is { } marked)
            {
                await PostgresParkingStructureRepository.Execute(db, tx, "INSERT INTO structure_slot_audit(id,site_id,slot_id,actor_id,action,reason) VALUES ($1,$2,$3,$4,$5,$6)", ct, Guid.NewGuid(), siteId, marked, owner.UserId, structure.Slots.Single(slot => slot.Id == marked).ReservationState == "BACKUP" ? "MARK_BACKUP" : "RELEASE_BACKUP", reason);
            }

            await using var ready = PostgresParkingStructureRepository.Command(db, tx, """
                UPDATE structure_synchronizations SET state='ready',outcome=$2::jsonb,updated_at=now()
                WHERE token=$1 AND state='preparing' AND site_id=$3 AND tenant_id=$4
                """, synchronizationToken,
                System.Text.Json.JsonSerializer.Serialize(outcome, StructureSynchronizationJson.Options),
                siteId, owner.TenantId);
            if (await ready.ExecuteNonQueryAsync(ct) != 1)
                throw new StructureException("STRUCTURE_CONFLICT", "Synchronization preparation is missing.");

            if (layoutChanged)
            {
                await txScope.StageAndCommitAsync(
                    layoutInvalidations,
                    site.TenantId,
                    siteId,
                    [LayoutInvalidationResources.Structure, LayoutInvalidationResources.FullLayout, LayoutInvalidationResources.CapacitySummary],
                    ct);
            }
            else
            {
                await txScope.CommitWithoutStagingAsync(ct);
            }
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
        await using var cmd = PostgresParkingStructureRepository.Command(db, tx, """
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
        await using var cmd = PostgresParkingStructureRepository.Command(db, tx, """
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
        await using var cmd = PostgresParkingStructureRepository.Command(db, tx, """
            SELECT id, path_code, deleted_at FROM parking_access_paths
            WHERE tenant_id=$1 AND site_id=$2 AND deleted_at IS NOT NULL AND upper(path_code)=upper($3)
            LIMIT 1
            """, structure.Site.TenantId, siteId, path.Code);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        return new(r.GetGuid(0), r.GetString(1), r.GetDateTime(2),
            "This path code belongs to a released path; restore it explicitly.");
    }

    public ValueTask DisposeAsync() => txScope.DisposeAsync();
}
