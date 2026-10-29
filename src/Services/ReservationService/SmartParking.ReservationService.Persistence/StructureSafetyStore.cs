using Microsoft.EntityFrameworkCore;
using SmartParking.ReservationService.Application.Common.Interfaces.Persistence;
using SmartParking.ReservationService.Domain;
using SmartParking.ReservationService.Domain.Entities;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SmartParking.ReservationService.Persistence;

public sealed class StructureSafetyStore(ReservationDbContext db) : IStructureSafetyStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<object> ImpactAsync(Guid tenantId, Guid siteId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.RepeatableRead, ct);
        var now = DateTimeOffset.UtcNow;
        var reservations = await db.Reservations.AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.SiteId == siteId &&
                ((r.Status == "PENDING_PAYMENT" && (r.HoldExpiresAt == null || r.HoldExpiresAt > now)) ||
                 (new[] { "CONFIRMED", "ALLOCATED", "PARKING" }.Contains(r.Status) && r.ExpectedEndTime > now)))
            .OrderBy(r => r.ExpectedStartTime)
            .ThenBy(r => r.ConfirmedAt)
            .ThenBy(r => r.Id)
            .Select(r => new
            {
                id = r.Id,
                status = r.Status,
                expectedStart = r.ExpectedStartTime,
                confirmedAt = r.ConfirmedAt,
                unitId = r.TargetSpatialUnitId,
                slotId = r.TargetSlotId
            }).ToListAsync(ct);
        var sessions = await db.ParkingSessions.AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.SiteId == siteId && s.ExitTime == null)
            .OrderBy(s => s.EntryTime)
            .ThenBy(s => s.Id)
            .Select(s => new { id = s.Id, slotId = s.CurrentSlotId })
            .ToListAsync(ct);
        await transaction.CommitAsync(ct);
        return new { reservations, sessions, priority = "expectedStartThenConfirmation", preference = "sameLotThenZone" };
    }

    public async Task<HoldSnapshot> AcquireAsync(Guid tenantId, Guid siteId, Guid token, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockSiteAsync(siteId, ct);
        if (await db.StructureReleaseReceipts.AnyAsync(r => r.Token == token, ct))
            throw new HoldConflictException();

        var hold = await db.StructureEditHolds.SingleOrDefaultAsync(h => h.SiteId == siteId, ct);
        if (hold is null)
        {
            db.StructureEditHolds.Add(new StructureEditHoldRow
            {
                SiteId = siteId,
                TenantId = tenantId,
                Token = token,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync(ct);
        }
        else if (hold.TenantId != tenantId || hold.Token != token)
        {
            throw new HoldConflictException();
        }

        var snapshot = await BuildSnapshotAsync(tenantId, siteId, ct);
        await transaction.CommitAsync(ct);
        return snapshot;
    }

    public async Task<HoldSnapshot> ReadSnapshotAsync(Guid tenantId, Guid siteId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.RepeatableRead, ct);
        var snapshot = await BuildSnapshotAsync(tenantId, siteId, ct);
        await transaction.CommitAsync(ct);
        return snapshot;
    }

    private async Task<HoldSnapshot> BuildSnapshotAsync(Guid tenantId, Guid siteId, CancellationToken ct)
    {
        var allocationSlots = db.SlotAllocations.AsNoTracking()
            .Where(a => a.SiteId == siteId && new[] { "RESERVED", "OCCUPIED" }.Contains(a.AllocationStatus))
            .Select(a => a.SlotId);
        var sessionSlots = db.ParkingSessions.AsNoTracking()
            .Where(s => s.SiteId == siteId && s.ExitTime == null && s.CurrentSlotId != null)
            .Select(s => s.CurrentSlotId!.Value);
        var protectedIds = await allocationSlots.Union(sessionSlots).ToArrayAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var live = await db.Reservations.AsNoTracking().AnyAsync(r => r.SiteId == siteId &&
            ((r.Status == "PENDING_PAYMENT" && (r.HoldExpiresAt == null || r.HoldExpiresAt > now)) ||
             (new[] { "CONFIRMED", "ALLOCATED", "PARKING" }.Contains(r.Status) && r.ExpectedEndTime > now)), ct)
            || await db.ParkingSessions.AsNoTracking().AnyAsync(s => s.SiteId == siteId && s.ExitTime == null, ct)
            || await db.SlotAllocations.AsNoTracking().AnyAsync(a => a.SiteId == siteId &&
                new[] { "RESERVED", "OCCUPIED" }.Contains(a.AllocationStatus), ct)
            || await db.SiteCapacityPools.AsNoTracking().AnyAsync(p => p.SiteId == siteId &&
                (p.CurrentReservedCount > 0 || p.PendingPaymentCount > 0 || p.OccupiedCount > 0 || p.ProtectedCount > 0), ct);
        var capacities = await db.SiteCapacityPools.AsNoTracking()
            .Where(p => p.SiteId == siteId && p.TenantId == tenantId)
            .Select(p => new CapacityClaim(p.SpatialUnitId, p.VehicleType, p.PendingPaymentCount, p.ProtectedCount))
            .ToArrayAsync(ct);
        return new(protectedIds, live, capacities);
    }

    public async Task ReleaseAsync(Guid tenantId, Guid siteId, Guid token, EditOutcome? outcome, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockSiteAsync(siteId, ct);
        var payload = JsonSerializer.Serialize(outcome, JsonOptions);
        var receipt = await db.StructureReleaseReceipts.SingleOrDefaultAsync(r => r.Token == token, ct);
        if (receipt is not null)
        {
            if (receipt.TenantId != tenantId || receipt.SiteId != siteId ||
                !JsonNode.DeepEquals(JsonNode.Parse(receipt.Outcome), JsonNode.Parse(payload)))
                throw new HoldConflictException();
            await transaction.CommitAsync(ct);
            return;
        }

        var hold = await db.StructureEditHolds.SingleOrDefaultAsync(h => h.SiteId == siteId, ct);
        if (hold is null)
        {
            if (outcome is not null)
                throw new HoldConflictException();
            AddReceipt(tenantId, siteId, token, payload);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return;
        }
        if (hold.TenantId != tenantId || hold.Token != token)
            throw new HoldConflictException();

        // Flush the hold deletion first: guard_structure_edit() rejects writes to
        // site_capacity_pools while a hold row for the site still exists.
        db.StructureEditHolds.Remove(hold);
        await db.SaveChangesAsync(ct);

        if (outcome is not null)
        {
            var state = await db.StructureSiteStates.SingleOrDefaultAsync(s => s.SiteId == siteId, ct);
            if (state is null)
                db.StructureSiteStates.Add(new StructureSiteStateRow { SiteId = siteId, TenantId = tenantId, Active = outcome.SiteActive });
            else
            {
                state.TenantId = tenantId;
                state.Active = outcome.SiteActive;
            }

            foreach (var (ids, kind) in new[] { (outcome.RemovedSlots, "slot"), (outcome.RemovedUnits, "unit") })
                foreach (var id in ids)
                    if (!await db.StructureRemovedResources.AnyAsync(r => r.Id == id, ct))
                        db.StructureRemovedResources.Add(new StructureRemovedResourceRow { Id = id, SiteId = siteId, Kind = kind });

            var pools = await db.SiteCapacityPools
                .Where(p => p.SiteId == siteId && p.TenantId == tenantId)
                .ToListAsync(ct);
            foreach (var pool in pools)
            {
                var capacity = outcome.Capacities.FirstOrDefault(c =>
                    c.UnitId == pool.SpatialUnitId && c.VehicleType == pool.VehicleType);
                pool.TotalCapacity = capacity?.Capacity ?? 0;
                pool.MaxReservationQuota = Math.Min(pool.MaxReservationQuota, pool.TotalCapacity);
                pool.BackupCount = capacity?.Backup ?? 0;
                pool.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        AddReceipt(tenantId, siteId, token, payload);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task ForceReleaseAsync(Guid tenantId, Guid siteId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockSiteAsync(siteId, ct);
        var hold = await db.StructureEditHolds.SingleOrDefaultAsync(h => h.SiteId == siteId, ct);
        if (hold is null)
        {
            await transaction.CommitAsync(ct);
            return;
        }
        // A force release may only drop a hold in the caller's tenant scope; a
        // different tenant must still surface as a conflict, never a silent repair.
        if (hold.TenantId != tenantId)
            throw new HoldConflictException();

        db.StructureEditHolds.Remove(hold);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private void AddReceipt(Guid tenantId, Guid siteId, Guid token, string payload) =>
        db.StructureReleaseReceipts.Add(new StructureReleaseReceiptRow
        {
            Token = token,
            TenantId = tenantId,
            SiteId = siteId,
            Outcome = payload,
            CompletedAt = DateTimeOffset.UtcNow
        });

    private Task<int> LockSiteAsync(Guid siteId, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({siteId.ToString()}, 0))", ct);
}
