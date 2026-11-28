using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using SmartParking.FacilityService.Application.Reservations;
using SmartParking.FacilityService.Domain;
using SmartParking.FacilityService.Domain.Entities;

namespace SmartParking.FacilityService.Persistence;

public sealed class ReservationSlotReader(ParkingDbContext db) : IReservationSlotReader
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

}
