using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Domain.Reservations;
using SmartParking.OperationService.Persistence;

namespace SmartParking.OperationService.Tests;

public sealed partial class QuoteBookingDatabaseTests
{
    [QuoteDatabaseFact]
    public async Task SpecificSlotAdmissionRefusesUnprovenScopedCapacityButIgnoresExpiredScopedHolds()
    {
        await using var services = Services();
        var first = await Prepare(services, Guid.NewGuid());
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();
        var quota = await db.Set<OperationalCapacityConfig>().SingleAsync(q => q.SiteId == site);
        quota.TotalPhysicalCapacity = quota.MaxReservationQuota = 3;
        var scoped = new ReservationRow { Id = Guid.NewGuid(), TenantId = tenant, SiteId = site,
            AccountId = Guid.NewGuid(), TargetSpatialUnitId = unit, VehicleType = "CAR",
            ExpectedStartTime = start, ExpectedEndTime = start.AddHours(1), CreatedAt = DateTimeOffset.UtcNow,
            Status = "PENDING_PAYMENT", HoldExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5) };
        db.Add(scoped);
        await db.SaveChangesAsync();
        await Send(services, new CommitQuoteHoldCommand(first));
        Assert.Equal("SCOPED_CAPACITY_EVIDENCE_REQUIRED",
            await db.ReservationRequests.Where(r => r.Id == first).Select(r => r.Code).SingleAsync());
        Assert.False(await db.Reservations.AnyAsync(r => r.RequestId == first));

        scoped.HoldExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        db.Add(new ReservationRow { Id = Guid.NewGuid(), TenantId = tenant, SiteId = site,
            AccountId = Guid.NewGuid(), TargetSlotId = slot, NormalizedPlate = plate, VehicleType = "CAR",
            Status = "CANCELLED", ExpectedStartTime = start, ExpectedEndTime = start.AddHours(1), CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        var next = await Prepare(services, Guid.NewGuid());
        await Send(services, new CommitQuoteHoldCommand(next));
        Assert.Equal("PENDING_PAYMENT", await db.Reservations.Where(r => r.Id == next).Select(r => r.Status).SingleAsync());
    }

    [QuoteDatabaseFact]
    public async Task AdmissionHonorsAllocationPastBookingEndUntilItsOwnEnd()
    {
        await using var services = Services();
        var first = await Prepare(services, Guid.NewGuid());
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();
        var prior = new ReservationRow { Id = Guid.NewGuid(), AccountId = Guid.NewGuid(), TenantId = tenant,
            SiteId = site, TargetSlotId = slot, NormalizedPlate = plate, VehicleType = "CAR", Status = "CONFIRMED",
            ExpectedStartTime = start.AddHours(-1), ExpectedEndTime = start, CreatedAt = DateTimeOffset.UtcNow };
        var assignment = new SlotAllocationRow { Id = Guid.NewGuid(), ReservationId = prior.Id, SlotId = slot,
            TenantId = tenant, SiteId = site, AllocatedStartTime = prior.ExpectedStartTime,
            AllocatedEndTime = start.AddMinutes(10), AllocationStatus = "RESERVED", CreatedAt = DateTimeOffset.UtcNow };
        db.Add(prior);
        await db.SaveChangesAsync();
        db.Add(assignment);
        await db.SaveChangesAsync();

        await Send(services, new CommitQuoteHoldCommand(first));
        Assert.Equal("SLOT_TIME_CONFLICT",
            await db.ReservationRequests.Where(r => r.Id == first).Select(r => r.Code).SingleAsync());
        Assert.False(await db.Reservations.AnyAsync(r => r.RequestId == first));

        assignment.AllocatedEndTime = start;
        await db.SaveChangesAsync();
        var next = await Prepare(services, Guid.NewGuid());
        await Send(services, new CommitQuoteHoldCommand(next));
        Assert.Equal("PENDING_PAYMENT", await db.Reservations.Where(r => r.Id == next).Select(r => r.Status).SingleAsync());
    }

    [QuoteDatabaseFact]
    public async Task AdmissionUsesScannedReplacementAndPreservesOriginalAndReleasedHistory()
    {
        await using var services = Services();
        var request = await Prepare(services, Guid.NewGuid());
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();
        var limit = await db.Set<OperationalCapacityConfig>().SingleAsync(q => q.SiteId == site);
        limit.TotalPhysicalCapacity = limit.MaxReservationQuota = 2;
        var now = DateTimeOffset.UtcNow;
        var otherSlot = Guid.NewGuid();
        var booking = new ReservationRow { Id = Guid.NewGuid(), TenantId = tenant, SiteId = site,
            AccountId = Guid.NewGuid(), TargetSlotId = slot, ActiveClaimSlotId = slot, VehicleType = "CAR",
            Status = "PARKING", ExpectedStartTime = now.AddHours(-1), ExpectedEndTime = start.AddHours(2), CreatedAt = now };
        db.Add(booking);
        await db.SaveChangesAsync();
        var visit = new ParkingSessionRow { Id = Guid.NewGuid(), ReservationId = booking.Id, AccountId = booking.AccountId,
            TenantId = tenant, SiteId = site, CurrentSlotId = otherSlot, EntryTime = now.AddMinutes(-20),
            LocationScannedAt = now, VehicleType = "CAR", SessionStatus = "ACTIVE", CreatedAt = now };
        db.Add(visit);
        await db.SaveChangesAsync();
        db.AddRange(new SlotAllocationRow { Id = Guid.NewGuid(), ReservationId = booking.Id, TenantId = tenant,
                SiteId = site, SlotId = slot, AllocatedStartTime = booking.ExpectedStartTime, AllocatedEndTime = now,
                AllocationStatus = "RELEASED", CreatedAt = booking.ExpectedStartTime },
            new SlotAllocationRow { Id = Guid.NewGuid(), ReservationId = booking.Id, ParkingSessionId = visit.Id,
                TenantId = tenant, SiteId = site, SlotId = otherSlot, AllocatedStartTime = now,
                AllocatedEndTime = booking.ExpectedEndTime, AllocationStatus = "OCCUPIED", CreatedAt = now });
        await db.SaveChangesAsync();
        await Send(services, new CommitQuoteHoldCommand(request));
        Assert.Equal("PENDING_PAYMENT", await db.Reservations.Where(r => r.Id == request).Select(r => r.Status).SingleAsync());
        Assert.Equal(slot, await db.Reservations.Where(r => r.Id == booking.Id).Select(r => r.TargetSlotId).SingleAsync());
        Assert.Equal(2, await db.SlotAllocations.CountAsync(a => a.ReservationId == booking.Id));
        Assert.True(await db.SlotAllocations.AnyAsync(a => a.ReservationId == booking.Id && a.SlotId == slot && a.AllocationStatus == "RELEASED"));
    }

    [QuoteDatabaseFact]
    public async Task FutureQuoteLeavesLegacyWalkInPhysicallyOpenAfterProjection()
    {
        await using var services = Services();
        var request = await Prepare(services, Guid.NewGuid());
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();
        var now = DateTimeOffset.UtcNow;
        var visit = new ParkingSessionRow { Id = Guid.NewGuid(), TenantId = tenant, SiteId = site,
            CurrentSlotId = slot, EntryTime = now.AddMinutes(-5), LocationScannedAt = now, NormalizedPlate = plate,
            VehicleType = "CAR", SessionStatus = "ACTIVE", CreatedAt = now };
        db.Add(visit);
        await db.SaveChangesAsync();
        db.Add(new SlotAllocationRow { Id = Guid.NewGuid(), ParkingSessionId = visit.Id, TenantId = tenant,
            SiteId = site, SlotId = slot, AllocatedStartTime = now, AllocatedEndTime = DateTimeOffset.MaxValue,
            AllocationStatus = "OCCUPIED", CreatedAt = now });
        await db.SaveChangesAsync();
        await Send(services, new CommitQuoteHoldCommand(request));
        Assert.Equal("PENDING_PAYMENT", await db.Reservations.Where(r => r.Id == request).Select(r => r.Status).SingleAsync());
        Assert.True(await db.ParkingSessions.AnyAsync(s => s.Id == visit.Id && s.ExitTime == null &&
            s.CurrentSlotId == slot && s.SessionStatus == "ACTIVE"));
        Assert.True(await db.SlotAllocations.AnyAsync(a => a.ParkingSessionId == visit.Id && a.AllocationStatus == "OCCUPIED"));
    }

    [QuoteDatabaseFact]
    public Task CompetingNamedSlotHoldsHaveOneWinner() => RaceCapacityAsync(sharedSlot: true, rollBackFirst: false);

    [QuoteDatabaseFact]
    public Task DifferentSlotsCompeteForLastCompatibleUnit() => RaceCapacityAsync(sharedSlot: false, rollBackFirst: false);

    [QuoteDatabaseFact]
    public Task RolledBackHoldReturnsLastCompatibleUnitToWaitingWriter() => RaceCapacityAsync(sharedSlot: false, rollBackFirst: true);

    [QuoteDatabaseFact]
    public Task StaggeredEarlyWriterCommits() => RaceCapacityAsync(false, false, staggered: true);

    [QuoteDatabaseFact]
    public Task StaggeredLateWriterCommitsFirst() => RaceCapacityAsync(false, false, staggered: true, lateFirst: true);

    [QuoteDatabaseFact]
    public Task StaggeredWriterRollsBack() => RaceCapacityAsync(false, true, staggered: true);

    [QuoteDatabaseFact]
    public Task BothStaggeredWritersCanCommitWithAContinuousSpare() =>
        RaceCapacityAsync(false, false, staggered: true, ordinarySpare: true);

    private async Task RaceCapacityAsync(bool sharedSlot, bool rollBackFirst, bool staggered = false,
        bool lateFirst = false, bool ordinarySpare = false)
    {
        await using var services = Services();
        using var first = services.CreateScope();
        using var second = services.CreateScope();
        var firstDb = first.ServiceProvider.GetRequiredService<ReservationDbContext>();
        var secondDb = second.ServiceProvider.GetRequiredService<ReservationDbContext>();
        var otherSlot = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        // Controlled supply only. Demand, advisory locks, accepted quotes, holds and outbox are real PostgreSQL rows.
        var resources = new[] { new CapacityResource(slot, unit, [], true, false),
            new CapacityResource(otherSlot, unit, [], true, false), new CapacityResource(Guid.NewGuid(), unit, [], true, !ordinarySpare) };
        firstDb.Add(new OperationalCapacityConfig { Id = Guid.NewGuid(), TenantId = tenant, SiteId = site,
            VehicleType = "CAR", IsActive = true, TotalPhysicalCapacity = 3, MaxReservationQuota = 3,
            CreatedAt = now, UpdatedAt = now });
        if (!sharedSlot)
            firstDb.Add(new ReservationRow { Id = Guid.NewGuid(), TenantId = tenant, SiteId = site,
                AccountId = Guid.NewGuid(), TargetSpatialUnitId = unit, ExpectedStartTime = start,
                ExpectedEndTime = start.AddHours(1), CreatedAt = now, Status = "CONFIRMED", VehicleType = "CAR" });
        await firstDb.SaveChangesAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = timeout.Token;
        var attemptIds = new List<Guid>();
        async Task<AvailabilityDecision> TryHold(IServiceProvider provider, string canonicalPlate, Guid selected)
        {
            var db = provider.GetRequiredService<ReservationDbContext>();
            var guards = provider.GetRequiredService<IOperationsConcurrency>();
            var account = Guid.NewGuid();
            var availability = provider.GetRequiredService<IReservationAvailabilityRepository>();
            await availability.LockAccountAsync(account, ct);
            await guards.LockVehicleAsync(canonicalPlate, ct);
            await guards.LockSiteAsync(site, ct);
            var bookings = provider.GetRequiredService<IReservationBookingRepository>();
            var at = await bookings.DatabaseNowAsync(ct);
            var request = ReservationRequest.AdmitQuote(Guid.NewGuid(), account, Guid.NewGuid(), "{}", "test", at);
            attemptIds.Add(request.Id);
            var from = staggered && selected == otherSlot ? start.AddMinutes(30) : start;
            var until = staggered && selected == slot ? start.AddMinutes(30) : start.AddHours(1);
            var row = ReservationRow.PrepareQuote(request.Id, account, canonicalPlate, tenant, site, selected,
                "PAYOS", from, until, until, at, at.AddMinutes(5), 100, 0, 100, "VND",
                "{}", Guid.NewGuid(), "q1", start, "tariff", "none", false);
            await bookings.RequirePlateAvailableAsync(row, at, ct);
            var facts = await availability.ReadAsync(row, at, ct);
            static CapacityDemand Demand(ReservationRow r) => new(r.Id, r.ActiveClaimSlotId ?? r.TargetSlotId,
                r.TargetSpatialUnitId, r.ExpectedStartTime, r.ClaimEndTime ?? r.ExpectedEndTime);
            var decision = ReservationAvailability.Evaluate(Demand(row), facts.Reservations.Select(Demand).ToArray(),
                [], resources, [], facts.Quotas, at, TimeSpan.Zero, hasScopedCapacityEvidence: true);
            if (!decision.Allowed) return decision;

            request.AcceptDecision(request.AttemptId, 1, Guid.NewGuid(), at, true, at);
            request.RecordPlate(true, canonicalPlate, 1, at);
            request.RecordSlot(true, site, tenant, "eligible");
            request.AttachQuoteHold(row.Id);
            db.Add(request);
            await bookings.AddAsync(row, ct);
            await provider.GetRequiredService<IReservationFactsPublisher>().HoldChangedAsync(new(Guid.NewGuid(), at,
                request.Id, row.Id, account, tenant, site, selected, from, row.ClaimEndTime!.Value,
                row.HoldExpiresAt!.Value, row.Status, row.Version), ct);
            await provider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
            return decision;
        }

        await using var tx1 = await firstDb.Database.BeginTransactionAsync(ct);
        Assert.True((await TryHold(first.ServiceProvider, "51A10001", lateFirst ? otherSlot : slot)).Allowed);
        await using var tx2 = await secondDb.Database.BeginTransactionAsync(ct);
        var pid = ((NpgsqlConnection)secondDb.Database.GetDbConnection()).ProcessID;
        var waiter = TryHold(second.ServiceProvider, "51B10002", sharedSlot || lateFirst ? slot : otherSlot);
        // Observe actual database contention before deciding the first transaction.
        while (!await firstDb.Database.SqlQuery<bool>(
            $"SELECT pg_backend_pid() = ANY(pg_blocking_pids({pid})) AS \"Value\"").SingleAsync(ct))
        {
            Assert.False(waiter.IsCompleted, "The competing writer bypassed the site guard.");
            await Task.Delay(10, ct);
        }
        if (rollBackFirst) await tx1.RollbackAsync(ct);
        else await tx1.CommitAsync(ct);
        var result = await waiter;
        Assert.Equal(rollBackFirst || ordinarySpare, result.Allowed);
        if (!result.Allowed) Assert.Equal(sharedSlot ? "SLOT_TIME_CONFLICT" : "CAPACITY_UNAVAILABLE", result.Rejection);
        await tx2.CommitAsync(ct);

        var winners = await secondDb.Reservations.AsNoTracking().Where(r => r.SiteId == site && r.QuoteId != null).ToArrayAsync(ct);
        if (ordinarySpare) Assert.Equal(2, winners.Length);
        else Assert.Equal(rollBackFirst ? "51B10002" : "51A10001", Assert.Single(winners).NormalizedPlate);
        var messages = await secondDb.Set<OutboxMessage>().Where(m => m.MessageType.Contains("ReservationHoldChangedV1") &&
            m.CorrelationId.HasValue && attemptIds.Contains(m.CorrelationId.Value)).Select(m => m.CorrelationId).ToListAsync(ct);
        Assert.Equal(winners.Select(w => (Guid?)w.Id).Order().ToArray(), messages.Order().ToArray());
        Assert.False(await secondDb.SlotAllocations.AnyAsync(a => a.SiteId == site, ct));
    }
}
