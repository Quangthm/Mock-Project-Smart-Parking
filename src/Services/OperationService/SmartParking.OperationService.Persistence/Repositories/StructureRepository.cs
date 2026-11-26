using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Domain;
using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Persistence.Repositories;

public sealed class StructureRepository(ReservationDbContext db) : IStructureRepository
{
    public Task<StructureEditHoldRow?> HoldAsync(Guid siteId, CancellationToken ct) => FreshAsync(db.StructureEditHolds.Where(x => x.SiteId == siteId), ct);
    public Task<StructureReleaseReceiptRow?> ReceiptAsync(Guid token, CancellationToken ct) =>
        db.StructureReleaseReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.Token == token, ct);
    public Task<StructureSiteStateRow?> StateAsync(Guid siteId, CancellationToken ct) => FreshAsync(db.StructureSiteStates.Where(x => x.SiteId == siteId), ct);
    public async Task<IReadOnlyList<SiteCapacityPoolRow>> PoolsAsync(Guid tenantId, Guid siteId, CancellationToken ct)
    {
        var rows = await db.SiteCapacityPools.Where(x => x.TenantId == tenantId && x.SiteId == siteId).ToListAsync(ct);
        foreach (var row in rows) await db.Entry(row).ReloadAsync(ct);
        return rows;
    }
    private async Task<T?> FreshAsync<T>(IQueryable<T> query, CancellationToken ct) where T : class
    {
        var row = await query.SingleOrDefaultAsync(ct);
        if (row is not null) await db.Entry(row).ReloadAsync(ct);
        return row;
    }
    public void AddHold(StructureEditHoldRow hold) => db.StructureEditHolds.Add(hold);
    public void RemoveHold(StructureEditHoldRow hold) => db.StructureEditHolds.Remove(hold);
    public void AddReceipt(StructureReleaseReceiptRow receipt) => db.StructureReleaseReceipts.Add(receipt);
    public void AddState(StructureSiteStateRow state) => db.StructureSiteStates.Add(state);

    private IQueryable<ReservationRow> Live(Guid tenantId, Guid siteId, DateTimeOffset now) =>
        db.Reservations.AsNoTracking().Where(r => r.TenantId == tenantId && r.SiteId == siteId &&
            (r.Status == "PARKING" || r.Status == "PENDING_PAYMENT" && (r.HoldExpiresAt == null || r.HoldExpiresAt > now) ||
                (r.Status == "CONFIRMED" || r.Status == "ALLOCATED") && (r.ClaimEndTime ?? r.ExpectedEndTime) > now));

    public async Task<HoldSnapshot> SnapshotAsync(Guid tenantId, Guid siteId, DateTimeOffset now, CancellationToken ct)
    {
        var allocations = db.SlotAllocations.AsNoTracking().Where(a => a.TenantId == tenantId && a.SiteId == siteId &&
            (a.AllocationStatus == "RESERVED" || a.AllocationStatus == "OCCUPIED")).Select(a => a.SlotId);
        var sessions = db.ParkingSessions.AsNoTracking().Where(s => s.TenantId == tenantId && s.SiteId == siteId && s.ExitTime == null);
        var claims = Live(tenantId, siteId, now).Select(r => r.ActiveClaimSlotId ??
            db.SlotAllocations.Where(a => a.ReservationId == r.Id && a.TenantId == tenantId && a.SiteId == siteId &&
                (a.AllocationStatus == "RESERVED" || a.AllocationStatus == "OCCUPIED"))
                .OrderByDescending(a => a.CreatedAt).Select(a => (Guid?)a.SlotId).FirstOrDefault() ?? r.TargetSlotId);
        var protectedIds = await allocations.Union(sessions.Where(s => s.CurrentSlotId != null).Select(s => s.CurrentSlotId!.Value))
            .Union(claims.Where(id => id != null).Select(id => id!.Value)).ToArrayAsync(ct);
        var pools = await db.SiteCapacityPools.AsNoTracking().Where(p => p.SiteId == siteId && p.TenantId == tenantId).ToArrayAsync(ct);
        var live = await Live(tenantId, siteId, now).AnyAsync(ct) || await sessions.AnyAsync(ct) ||
            await allocations.AnyAsync(ct) || pools.Any(p => p.CurrentReservedCount > 0 || p.PendingPaymentCount > 0 || p.OccupiedCount > 0 || p.ProtectedCount > 0);
        return new(protectedIds, live, pools.Select(p => new CapacityClaim(p.SpatialUnitId, p.VehicleType, p.PendingPaymentCount, p.ProtectedCount)).ToArray());
    }

    public async Task<StructureImpactResponse> ImpactAsync(Guid tenantId, Guid siteId, DateTimeOffset now, CancellationToken ct)
    {
        var reservations = await Live(tenantId, siteId, now).OrderBy(r => r.ExpectedStartTime).ThenBy(r => r.ConfirmedAt).ThenBy(r => r.Id)
            .Select(r => new ReservationImpactItem(r.Id, r.Status, r.ExpectedStartTime, r.ConfirmedAt, r.TargetSpatialUnitId,
                r.ActiveClaimSlotId ?? db.SlotAllocations.Where(a => a.ReservationId == r.Id && a.TenantId == tenantId && a.SiteId == siteId &&
                    (a.AllocationStatus == "RESERVED" || a.AllocationStatus == "OCCUPIED"))
                    .OrderByDescending(a => a.CreatedAt).Select(a => (Guid?)a.SlotId).FirstOrDefault() ?? r.TargetSlotId)).ToListAsync(ct);
        var sessions = await db.ParkingSessions.AsNoTracking().Where(s => s.TenantId == tenantId && s.SiteId == siteId && s.ExitTime == null)
            .OrderBy(s => s.EntryTime).ThenBy(s => s.Id).Select(s => new SessionImpactItem(s.Id, s.CurrentSlotId)).ToListAsync(ct);
        return new(reservations, sessions);
    }
}
