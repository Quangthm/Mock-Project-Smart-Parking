using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using SmartParking.FacilityService.Application.Reservations;
using SmartParking.FacilityService.Domain;
using SmartParking.FacilityService.Domain.Entities;

namespace SmartParking.FacilityService.Persistence;

public sealed class ReservationDependencyRepository(ParkingDbContext db) : IReservationDependencyRepository
{
    public Task<SelectedSlotMetadata?> FindSlotMetadataAsync(Guid slotId, CancellationToken ct) =>
        db.ParkingSlotsFull.AsNoTracking()
            .Where(slot => slot.Id == slotId && slot.DeletedAt == null)
            .Select(slot => new SelectedSlotMetadata(slot.Id, slot.SiteId,
                db.ParkingSites.Any(site => site.Id == slot.SiteId && site.TenantId == slot.TenantId && site.DeletedAt == null) &&
                db.Tenants.Any(tenant => tenant.Id == slot.TenantId && tenant.DeletedAt == null) ? slot.TenantId : Guid.Empty))
            .SingleOrDefaultAsync(ct);

    // One statement gives scope, ancestry and restriction evidence from the same database snapshot.
    public Task<ReservationSlotEligibility?> CheckSlotEligibilityAsync(
        Guid slotId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct) =>
        db.Database.SqlQuery<ReservationSlotEligibility>($"""
            WITH RECURSIVE selected AS (
                SELECT * FROM parking_slots WHERE id={slotId} AND deleted_at IS NULL
            ), ancestors AS (
                SELECT u.id,u.parent_id,u.site_id,u.tenant_id,u.deleted_at
                FROM spatial_units u JOIN selected s ON u.id=s.spatial_unit_id
                UNION ALL
                SELECT u.id,u.parent_id,u.site_id,u.tenant_id,u.deleted_at
                FROM spatial_units u JOIN ancestors a ON u.id=a.parent_id
            ) CYCLE id SET cyclic USING ancestry,
            restrictions AS (
                SELECT w.* FROM structure_maintenance_windows w JOIN selected s ON w.site_id=s.site_id
                WHERE w.cancelled_at IS NULL AND w.service_window && tstzrange({start},{end},'[)')
                    AND (w.slot_id=s.id OR w.slot_id IS NULL AND
                        (w.unit_id IS NULL OR EXISTS(SELECT 1 FROM ancestors a WHERE a.id=w.unit_id)))
            ), decision AS (
                SELECT s.id,s.site_id,s.tenant_id,CASE
                    WHEN p.id IS NULL OR t.id IS NULL OR s.site_id='00000000-0000-0000-0000-000000000000'
                        OR s.tenant_id='00000000-0000-0000-0000-000000000000'
                        OR NOT EXISTS (SELECT 1 FROM ancestors WHERE parent_id IS NULL)
                        OR EXISTS (SELECT 1 FROM ancestors WHERE cyclic OR site_id<>s.site_id OR tenant_id<>s.tenant_id)
                        OR EXISTS (SELECT 1 FROM slot_compatibilities c WHERE c.slot_id=s.id
                            AND (c.site_id<>s.site_id OR c.tenant_id<>s.tenant_id))
                        THEN 'DEPENDENCY_UNAVAILABLE'
                    WHEN p.deleted_at IS NOT NULL OR t.deleted_at IS NOT NULL
                        OR p.status<>'ACTIVE' OR t.status<>'ACTIVE'
                        OR EXISTS (SELECT 1 FROM ancestors WHERE deleted_at IS NOT NULL)
                        OR s.physical_state IN ('MAINTENANCE','UNAVAILABLE') OR s.reservation_state='BACKUP'
                        OR NOT COALESCE((SELECT bool_or(c.vehicle_type='CAR') FROM slot_compatibilities c
                            WHERE c.slot_id=s.id), s.supported_vehicle_type='CAR', false)
                        THEN 'INELIGIBLE'
                    WHEN EXISTS (SELECT 1 FROM restrictions w WHERE w.tenant_id=s.tenant_id
                        AND w.unit_id IS NULL AND w.slot_id IS NULL AND w.enforcement_state='acknowledged'
                        AND lower_inc(w.service_window) AND NOT upper_inc(w.service_window) AND NOT upper_inf(w.service_window))
                        THEN 'INELIGIBLE'
                    WHEN s.physical_state='UNKNOWN' OR EXISTS (SELECT 1 FROM restrictions)
                        THEN 'DEPENDENCY_UNAVAILABLE'
                    ELSE 'ELIGIBLE'
                END AS outcome
                FROM selected s LEFT JOIN parking_sites p ON p.id=s.site_id AND p.tenant_id=s.tenant_id
                LEFT JOIN tenants t ON t.id=s.tenant_id
            )
            SELECT id AS "SlotId", outcome AS "Outcome",
                CASE WHEN outcome='ELIGIBLE' THEN site_id END AS "SiteId",
                CASE WHEN outcome='ELIGIBLE' THEN tenant_id END AS "TenantId"
            FROM decision
            """).SingleOrDefaultAsync(ct);

    public async Task LockAsync(Guid token, Guid siteId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Dependency requires the owning UoW transaction.");
        // Same site key as existing structure management. Never wait on a site whose editor may
        // be calling Operations: busy returns immediately and durable Operations work retries.
        await using var command = Command("SELECT pg_try_advisory_xact_lock(hashtextextended($1::text,2))", siteId);
        if (!Equals(await command.ExecuteScalarAsync(ct), true))
            throw new StructureException("STRUCTURE_BUSY", "Another request is managing this site.");
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({token.ToString()},5))", ct);
    }
    public Task<ReservationDependency?> FindAsync(Guid token, CancellationToken ct) =>
        db.Set<ReservationDependency>().SingleOrDefaultAsync(x => x.Id == token, ct);
    public Task<bool> HasHeldAsync(Guid siteId, CancellationToken ct) =>
        db.Set<ReservationDependency>().AsNoTracking().AnyAsync(x => x.SiteId == siteId && x.State == "HELD", ct);
    public Task<bool> HasPendingStructureEditAsync(Guid siteId, CancellationToken ct) =>
        db.StructureSynchronizations.AsNoTracking().AnyAsync(x => x.SiteId == siteId && x.State != "completed", ct);
    public void Add(ReservationDependency dependency) => db.Set<ReservationDependency>().Add(dependency);
    public async Task<DateTimeOffset> NowAsync(CancellationToken ct)
    {
        await using var command = Command("SELECT clock_timestamp()");
        var value = await command.ExecuteScalarAsync(ct);
        return value is DateTimeOffset offset ? offset : new DateTimeOffset((DateTime)value!);
    }
    private NpgsqlCommand Command(string sql, params object[] args)
    {
        var transaction = db.Database.CurrentTransaction ?? throw new InvalidOperationException("Owning transaction required.");
        var command = new NpgsqlCommand(sql, (NpgsqlConnection)db.Database.GetDbConnection(),
            (NpgsqlTransaction)transaction.GetDbTransaction());
        foreach (var arg in args) command.Parameters.AddWithValue(arg);
        return command;
    }
    public async Task<ResourceSnapshot> SnapshotAsync(Guid siteId, Guid slotId, CancellationToken ct)
    {
        var site = await db.ParkingSites.AsNoTracking().SingleOrDefaultAsync(s => s.Id == siteId, ct)
            ?? throw new StructureException("SITE_NOT_FOUND", "Site was not found.");
        var active = site.Status == "ACTIVE" && site.DeletedAt is null &&
            await db.Tenants.AsNoTracking().AnyAsync(t => t.Id == site.TenantId && t.Status == "ACTIVE" && t.DeletedAt == null, ct);
        var resources = new List<ResourceFact>();
        // MVP eligibility uses live unit ancestry and operational status, not path reachability.
        // A compatible mixed-category slot remains one resource ID.
        await using (var command = Command("""
            SELECT s.id,s.spatial_unit_id,s.supported_vehicle_type,
                s.physical_state IN ('AVAILABLE','OCCUPIED'),s.reservation_state='BACKUP',
                ARRAY(SELECT a.id FROM spatial_units a WHERE a.site_id=s.site_id
                    AND a.tenant_id=s.tenant_id AND u.path LIKE a.path || '%' ORDER BY a.path),
                NOT EXISTS(SELECT 1 FROM spatial_units a WHERE a.site_id=s.site_id
                    AND a.tenant_id=s.tenant_id AND u.path LIKE a.path || '%' AND a.deleted_at IS NOT NULL),
                ARRAY(SELECT c.vehicle_type FROM slot_compatibilities c WHERE c.slot_id=s.id ORDER BY c.vehicle_type)
            FROM parking_slots s JOIN spatial_units u ON u.id=s.spatial_unit_id
                AND u.site_id=s.site_id AND u.tenant_id=s.tenant_id
            WHERE s.site_id=$1 AND s.tenant_id=$2 AND s.deleted_at IS NULL AND u.deleted_at IS NULL
            ORDER BY s.id
            """, siteId, site.TenantId))
        await using (var reader = await command.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct))
            {
                var compatible = reader.GetFieldValue<string[]>(7);
                var primary = reader.IsDBNull(2) ? null : reader.GetString(2);
                if (compatible.Length == 0)
                {
                    if (primary is null) continue; // No supported category on this legacy slot.
                    compatible = [primary];
                }
                // This slice sells CAR only. Other categories retain identities for accounting.
                var category = compatible.Contains("CAR") ? "CAR" : compatible[0];
                var operational = active && reader.GetBoolean(3) && reader.GetBoolean(6);
                resources.Add(new(reader.GetGuid(0), reader.GetGuid(1), category, operational,
                    operational, !reader.IsDBNull(4) && reader.GetBoolean(4), reader.GetFieldValue<Guid[]>(5),
                    compatible));
            }
        var backups = await db.StructureBackupPolicies.AsNoTracking().Where(x => x.SiteId == siteId)
            .OrderBy(x => x.UnitId).ThenBy(x => x.VehicleType)
            .Select(x => new BackupFact(x.UnitId, x.VehicleType, x.Count)).ToArrayAsync(ct);
        var version = DependencyProtocol.Hash(JsonSerializer.Serialize(new { site.TenantId, site.Id, active, resources, backups }));
        var eligible = resources.Any(x => x.SlotId == slotId && x.VehicleType == "CAR" && x.Active && x.Reachable);
        return new(new(site.TenantId, siteId, slotId, version, eligible, true), resources, backups, true);
    }
}
