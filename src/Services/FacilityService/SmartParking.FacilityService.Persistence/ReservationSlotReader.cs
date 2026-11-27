using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartParking.FacilityService.Application.Reservations;

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
        db.Database.SqlQueryRaw<ReservationSlotEligibility>("""
            SELECT s.id AS "SlotId", eligibility.outcome AS "Outcome",
                CASE WHEN eligibility.outcome='ELIGIBLE' THEN s.site_id END AS "SiteId",
                CASE WHEN eligibility.outcome='ELIGIBLE' THEN s.tenant_id END AS "TenantId"
            FROM parking_slots s
            """ + "\n" + SlotEligibilitySql.Decision + "\nWHERE s.id=@slot AND s.deleted_at IS NULL",
            new NpgsqlParameter("slot", slotId), new NpgsqlParameter("start", start),
            new NpgsqlParameter("end", end)).SingleOrDefaultAsync(ct);
}
