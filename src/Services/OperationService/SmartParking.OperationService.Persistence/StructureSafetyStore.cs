using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Domain;
using SmartParking.OperationService.Domain.Entities;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SmartParking.OperationService.Persistence;

public sealed class StructureSafetyStore(ReservationDbContext db) : IStructureSafetyStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Serializable transactions can be aborted by PostgreSQL under genuine contention (40001) or a
    // detected deadlock (40P01). Each retry opens a fresh transaction, so a second attempt observes
    // the winner's committed state instead of the aborted snapshot. The bound is deliberately small:
    // the caller treats an exhausted retry as an ordinary hold conflict.
    private const int MaxTransactionAttempts = 3;
    private static readonly TimeSpan RetryBaseDelay = TimeSpan.FromMilliseconds(25);

    public async Task<StructureImpactResponse> ImpactAsync(Guid tenantId, Guid siteId, CancellationToken ct)
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
            .Select(r => new ReservationImpactItem(
                r.Id,
                r.Status,
                r.ExpectedStartTime,
                r.ConfirmedAt,
                r.TargetSpatialUnitId,
                r.TargetSlotId
            )).ToListAsync(ct);
        var sessions = await db.ParkingSessions.AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.SiteId == siteId && s.ExitTime == null)
            .OrderBy(s => s.EntryTime)
            .ThenBy(s => s.Id)
            .Select(s => new SessionImpactItem(s.Id, s.CurrentSlotId))
            .ToListAsync(ct);
        await transaction.CommitAsync(ct);
        return new StructureImpactResponse(reservations, sessions);
    }

    public Task<HoldSnapshot> AcquireAsync(Guid tenantId, Guid siteId, Guid token, CancellationToken ct) =>
        RetryTransactionAsync(ct, async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, ct);
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
                await SaveAsync(ct);
            }
            else if (hold.TenantId != tenantId || hold.Token != token)
            {
                throw new HoldConflictException();
            }

            var snapshot = await BuildSnapshotAsync(tenantId, siteId, ct);
            await transaction.CommitAsync(ct);
            return snapshot;
        });

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

    public Task ReleaseAsync(Guid tenantId, Guid siteId, Guid token, EditOutcome? outcome, CancellationToken ct) =>
        RetryTransactionAsync(ct, async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, ct);
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
                await SaveAsync(ct);
                await transaction.CommitAsync(ct);
                return;
            }
            if (hold.TenantId != tenantId || hold.Token != token)
                throw new HoldConflictException();

            // Capacity triggers reject writes while a hold exists. Flush its deletion
            // before changing pools; the transaction and serializable isolation keep release atomic.
            db.StructureEditHolds.Remove(hold);
            await SaveAsync(ct);

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

                // Pool triggers must observe the published site/resource state.
                await SaveAsync(ct);

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
            await SaveAsync(ct);
            await transaction.CommitAsync(ct);
        });

    public Task ForceReleaseAsync(Guid tenantId, Guid siteId, CancellationToken ct) =>
        RetryTransactionAsync(ct, async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, ct);
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

            // Ghi nhận biên nhận bồi hoàn (Compensated Abort Receipt) để bảo toàn Correlation ID và Audit Trail
            var abortPayload = JsonSerializer.Serialize(new
            {
                status = "COMPENSATED_ABORT",
                reason = "Force released by saga compensation"
            }, JsonOptions);
            AddReceipt(tenantId, siteId, hold.Token, abortPayload);

            db.StructureEditHolds.Remove(hold);
            await SaveAsync(ct);
            await transaction.CommitAsync(ct);
        });

    // EF wraps provider failures (DbUpdateException, or InvalidOperationException for transient
    // errors) and keeps the failed unit of work tracked. The structure-edit flow is written against
    // the database exception, and a retry after a rolled-back projection must start from the
    // committed state, so surface the PostgresException and reset the tracker.
    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (Exception error) when (DatabaseException(error) is { } postgres)
        {
            db.ChangeTracker.Clear();
            throw postgres;
        }
    }

    private static PostgresException? DatabaseException(Exception error) => error switch
    {
        PostgresException postgres => postgres,
        DbUpdateException { InnerException: { } inner } => DatabaseException(inner),
        InvalidOperationException { InnerException: { } inner } => DatabaseException(inner),
        _ => null
    };

    // PostgreSQL aborts a serializable transaction with 40001 (serialization_failure) or 40P01
    // (deadlock_detected) under genuine contention. The aborted transaction is unusable, so each
    // attempt re-runs the whole unit of work in a NEW transaction after clearing the tracker.
    // Exhausted retries surface the existing HoldConflictException rather than a raw Npgsql error.
    private async Task<T> RetryTransactionAsync<T>(CancellationToken ct, Func<Task<T>> operation)
    {
        for (var attempt = 1; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                return await operation();
            }
            catch (Exception error) when (IsTransactionConflict(error))
            {
                db.ChangeTracker.Clear();
                if (attempt >= MaxTransactionAttempts)
                    throw new HoldConflictException();
                await Task.Delay(RetryDelay(attempt), ct);
            }
        }
    }

    private Task RetryTransactionAsync(CancellationToken ct, Func<Task> operation)
        => RetryTransactionAsync(ct, async () =>
        {
            await operation();
            return true;
        });

    private static bool IsTransactionConflict(Exception error)
    {
        var postgres = DatabaseException(error);
        return postgres is not null &&
            (postgres.SqlState == PostgresErrorCodes.SerializationFailure ||
             postgres.SqlState == PostgresErrorCodes.DeadlockDetected ||
             postgres.SqlState == PostgresErrorCodes.UniqueViolation ||
             postgres.SqlState == "55P03");
    }

    // 25 ms then 50 ms, plus up to 25 ms of jitter so competing retries do not stay in lockstep.
    private static TimeSpan RetryDelay(int attempt)
        => RetryBaseDelay * Math.Pow(2, attempt - 1) + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 25));

    private void AddReceipt(Guid tenantId, Guid siteId, Guid token, string payload) =>
        db.StructureReleaseReceipts.Add(new StructureReleaseReceiptRow
        {
            Token = token,
            TenantId = tenantId,
            SiteId = siteId,
            Outcome = payload,
            CompletedAt = DateTimeOffset.UtcNow
        });

    // Intentionally inert: serialization is provided by IsolationLevel.Serializable plus the bounded
    // retry in RetryTransactionAsync. Do not reintroduce an advisory lock here.
    private static Task LockSiteAsync(Guid siteId, CancellationToken ct) =>
        Task.CompletedTask;
}
