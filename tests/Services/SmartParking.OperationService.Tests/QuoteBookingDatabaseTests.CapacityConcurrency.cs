using MassTransit.EntityFrameworkCoreIntegration;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Domain.Reservations;
using SmartParking.OperationService.Persistence;

namespace SmartParking.OperationService.Tests;

public sealed partial class QuoteBookingDatabaseTests
{
    [QuoteDatabaseFact]
    public async Task EligibleSlotAdmitsWithUnlocatedWalkInAndCancelledClaim()
    {
        await using var services = Services();
        var request = await Prepare(services, Guid.NewGuid());
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();
        var now = DateTimeOffset.UtcNow;
        db.Add(new ParkingSessionRow { Id = Guid.NewGuid(), TenantId = tenant, SiteId = site, VehicleType = "CAR",
            EntryTime = now, SessionStatus = "ACTIVE", CreatedAt = now });
        db.Add(new ReservationRow { Id = Guid.NewGuid(), TenantId = tenant, SiteId = site,
            AccountId = Guid.NewGuid(), TargetSlotId = slot, NormalizedPlate = plate, VehicleType = "CAR",
            Status = "CANCELLED", ExpectedStartTime = start, ExpectedEndTime = start.AddHours(1), CreatedAt = now });
        await db.SaveChangesAsync();
        await Send(services, new CommitQuoteHoldCommand(request));
        Assert.Equal("PENDING_PAYMENT", await db.Reservations.Where(r => r.Id == request).Select(r => r.Status).SingleAsync());
        Assert.False(await db.SlotAllocations.AnyAsync(a => a.SiteId == site));
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
    public Task CompetingNamedSlotHoldsHaveOneWinner() => RaceCapacityAsync(sharedSlot: true);

    [QuoteDatabaseFact]
    public Task IndependentNamedSlotHoldsBothCommit() => RaceCapacityAsync();

    [QuoteDatabaseFact]
    public Task RolledBackNamedHoldLetsWaitingWriterCommit() => RaceCapacityAsync(sharedSlot: true, rollBackFirst: true);

    [QuoteDatabaseFact]
    public Task CompetingSameSitePlateHoldsHaveOneWinnerAcrossSlots() => RaceCapacityAsync(sharedPlate: true);

    [QuoteDatabaseFact]
    public Task AdjacentNamedHoldsBothCommitOnTheSameSlot() => RaceCapacityAsync(sharedSlot: true, adjacent: true);

    [QuoteDatabaseFact]
    public Task SiteCommercialQuotaStillSerializesIndependentSlots() => RaceCapacityAsync(quota: 1);

    private async Task RaceCapacityAsync(bool sharedSlot = false, bool rollBackFirst = false,
        bool sharedPlate = false, bool adjacent = false, int quota = 2)
    {
        await using var services = Services();
        using var first = services.CreateScope();
        using var second = services.CreateScope();
        var firstDb = first.ServiceProvider.GetRequiredService<ReservationDbContext>();
        var secondDb = second.ServiceProvider.GetRequiredService<ReservationDbContext>();
        var otherSlot = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        firstDb.Add(new OperationalCapacityConfig { Id = Guid.NewGuid(), TenantId = tenant, SiteId = site,
            VehicleType = "CAR", IsActive = true, TotalPhysicalCapacity = 2, MaxReservationQuota = quota,
            CreatedAt = now, UpdatedAt = now });
        await firstDb.SaveChangesAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = timeout.Token;
        async Task<(Guid RequestId, string? Rejection)> TryHold(IServiceProvider provider, string canonicalPlate, Guid selected,
            DateTimeOffset from, DateTimeOffset until)
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
            var row = ReservationRow.PrepareQuote(request.Id, account, canonicalPlate, tenant, site, selected,
                "PAYOS", from, until, until, at, at.AddMinutes(5), 100, 0, 100, "VND",
                """{"Values":{"OCCUPANCY_VALIDITY_TIMESPAN":240}}""", Guid.NewGuid(), start, "tariff", "none", false);
            try
            {
                await bookings.RequirePlateAvailableAsync(row, at, ct);
                await provider.GetRequiredService<IMediator>().Send(new RequireReservationAvailabilityCommand(row, at), ct);
            }
            catch (ReservationGateException error)
            {
                return (request.Id, error.Code);
            }

            request.AcceptDecision(request.AttemptId, 1, Guid.NewGuid(), at, true, at);
            request.RecordPlate(true, canonicalPlate, 1, at);
            request.RecordSlot(true, site, tenant);
            request.CompleteAdmission(row.Id, false);
            db.Add(request);
            await bookings.AddAsync(row, ct);
            await provider.GetRequiredService<IReservationFactsPublisher>().HoldChangedAsync(new(Guid.NewGuid(), at,
                request.Id, row.Id, account, tenant, site, selected, from, row.ClaimEndTime!.Value,
                row.HoldExpiresAt!.Value, row.Status, row.Version), ct);
            await provider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
            return (request.Id, null);
        }

        await using var tx1 = await firstDb.Database.BeginTransactionAsync(ct);
        var end = start.AddHours(1);
        var firstResult = await TryHold(first.ServiceProvider, "51A10001", slot, start, end);
        Assert.Null(firstResult.Rejection);
        await using var tx2 = await secondDb.Database.BeginTransactionAsync(ct);
        var pid = ((NpgsqlConnection)secondDb.Database.GetDbConnection()).ProcessID;
        var waiter = TryHold(second.ServiceProvider, sharedPlate ? "51A10001" : "51B10002", sharedSlot ? slot : otherSlot,
            adjacent ? end : start, adjacent ? end.AddHours(1) : end);
        // Observe actual database contention before deciding the first transaction.
        while (!await firstDb.Database.SqlQuery<bool>(
            $"SELECT pg_backend_pid() = ANY(pg_blocking_pids({pid})) AS \"Value\"").SingleAsync(ct))
        {
            Assert.False(waiter.IsCompleted, "The competing writer bypassed the plate or site guard.");
            await Task.Delay(10, ct);
        }
        if (rollBackFirst) await tx1.RollbackAsync(ct);
        else await tx1.CommitAsync(ct);
        var result = await waiter;
        var expected = (rollBackFirst, adjacent, sharedPlate, sharedSlot, quota) switch
        {
            (true, _, _, _, _) or (_, true, _, _, _) => null,
            (_, _, true, _, _) => "VEHICLE_TIME_CONFLICT",
            (_, _, _, true, _) => "SLOT_TIME_CONFLICT",
            (_, _, _, _, 1) => "RESERVATION_QUOTA_EXCEEDED",
            _ => null
        };
        Assert.Equal(expected, result.Rejection);
        await tx2.CommitAsync(ct);

        var winners = await secondDb.Reservations.AsNoTracking().Where(r => r.SiteId == site && r.QuoteId != null).ToArrayAsync(ct);
        var expectedHoldIds = new[] { (Id: firstResult.RequestId, Kept: !rollBackFirst),
            (Id: result.RequestId, Kept: expected is null) }.Where(h => h.Kept).Select(h => h.Id).Order().ToArray();
        Assert.Equal(expectedHoldIds, winners.Select(w => w.Id).Order().ToArray());
        Guid?[] attemptIds = [firstResult.RequestId, result.RequestId];
        var messages = await secondDb.Set<OutboxMessage>().Where(m => m.MessageType.Contains("ReservationHoldChangedV1") &&
            attemptIds.Contains(m.CorrelationId)).Select(m => m.CorrelationId).ToListAsync(ct);
        Assert.Equal(winners.Select(w => (Guid?)w.Id).Order().ToArray(), messages.Order().ToArray());
        Assert.False(await secondDb.SlotAllocations.AnyAsync(a => a.SiteId == site, ct));
    }
}
