using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.OperationService.API.Middleware;
using SmartParking.OperationService.Application.Usecase.Structure.Commands.ApplyMaintenanceWindow;
using SmartParking.OperationService.Application.Usecase.Structure.Commands.CancelMaintenanceWindow;
using SmartParking.OperationService.Domain;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Persistence;
using SmartParking.OperationService.Persistence.Repositories;
using SmartParking.OperationService.Tests.TestDoubles;

namespace SmartParking.OperationService.Tests;

public sealed class MaintenanceClosureEnforcementTests
{
    private static readonly DateTimeOffset WindowStart = new(2026, 11, 22, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset WindowEnd = WindowStart.AddHours(4);
    private static readonly DateTimeOffset Now = WindowStart.AddHours(-1);
    private static readonly CancellationToken Ct = CancellationToken.None;

    // ---- A. Half-open boundary and identity ------------------------------------------------

    [Fact]
    public async Task ReservationEndingExactlyAtWindowStart_IsNotAConflict()
    {
        var (tenantId, siteId) = (Guid.NewGuid(), Guid.NewGuid());
        var reservation = Reservation(tenantId, siteId, WindowStart.AddHours(-2), WindowStart);
        await using var db = await CreateDbAsync(reservation);

        var conflicts = await ConflictsAsync(db, tenantId, siteId, WindowStart, WindowEnd, Now);

        Assert.Empty(conflicts);
    }

    [Fact]
    public async Task ReservationStartingExactlyAtWindowEnd_IsNotAConflict()
    {
        var (tenantId, siteId) = (Guid.NewGuid(), Guid.NewGuid());
        var reservation = Reservation(tenantId, siteId, WindowEnd, WindowEnd.AddHours(2));
        await using var db = await CreateDbAsync(reservation);

        var conflicts = await ConflictsAsync(db, tenantId, siteId, WindowStart, WindowEnd, Now);

        Assert.Empty(conflicts);
    }

    [Fact]
    public async Task OverlappingReservation_ReportsExactReservationIdentity()
    {
        var (tenantId, siteId) = (Guid.NewGuid(), Guid.NewGuid());
        var unitId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var reservation = Reservation(tenantId, siteId, WindowStart.AddHours(1), WindowEnd.AddMinutes(30),
            unitId: unitId, targetSlotId: slotId);
        await using var db = await CreateDbAsync(reservation);

        var conflicts = await ConflictsAsync(db, tenantId, siteId, WindowStart, WindowEnd, Now);

        var conflict = Assert.Single(conflicts);
        Assert.Equal("RESERVATION", conflict.Type);
        Assert.Equal(reservation.Id, conflict.Id);
        Assert.Equal(reservation.ExpectedStartTime, conflict.Start);
        Assert.Equal(reservation.ExpectedEndTime, conflict.End);
        Assert.Equal(unitId, conflict.UnitId);
        Assert.Equal(slotId, conflict.SlotId);
    }

    // ---- B. Open sessions ----------------------------------------------------------------

    [Fact]
    public async Task OpenSessionOverlappingWindow_IsReportedAsSessionWithNullEnd()
    {
        var (tenantId, siteId) = (Guid.NewGuid(), Guid.NewGuid());
        var slotId = Guid.NewGuid();
        var session = new ParkingSessionRow
        {
            Id = Guid.NewGuid(), TenantId = tenantId, SiteId = siteId, CurrentSlotId = slotId,
            EntryTime = WindowStart.AddHours(-1), CreatedAt = WindowStart.AddHours(-2), SessionStatus = "ACTIVE"
        };
        await using var db = await CreateDbAsync(session);

        var conflicts = await ConflictsAsync(db, tenantId, siteId, WindowStart, WindowEnd, Now);

        var conflict = Assert.Single(conflicts);
        Assert.Equal("SESSION", conflict.Type);
        Assert.Equal(session.Id, conflict.Id);
        Assert.Equal(session.EntryTime, conflict.Start);
        Assert.Null(conflict.End);
        Assert.Equal(slotId, conflict.SlotId);
    }

    [Fact]
    public async Task SessionWithExitTime_IsNotAConflict()
    {
        var (tenantId, siteId) = (Guid.NewGuid(), Guid.NewGuid());
        var session = new ParkingSessionRow
        {
            Id = Guid.NewGuid(), TenantId = tenantId, SiteId = siteId, CurrentSlotId = Guid.NewGuid(),
            EntryTime = WindowStart.AddHours(-1), ExitTime = WindowStart.AddMinutes(30),
            CreatedAt = WindowStart.AddHours(-2), SessionStatus = "COMPLETED"
        };
        await using var db = await CreateDbAsync(session);

        var conflicts = await ConflictsAsync(db, tenantId, siteId, WindowStart, WindowEnd, Now);

        Assert.Empty(conflicts);
    }

    [Fact]
    public async Task ParkingStatusReservation_IsAConflict()
    {
        var (tenantId, siteId) = (Guid.NewGuid(), Guid.NewGuid());
        var reservation = Reservation(tenantId, siteId, WindowStart.AddHours(1), WindowEnd, status: "PARKING");
        await using var db = await CreateDbAsync(reservation);

        var conflicts = await ConflictsAsync(db, tenantId, siteId, WindowStart, WindowEnd, Now);

        var conflict = Assert.Single(conflicts);
        Assert.Equal("RESERVATION", conflict.Type);
        Assert.Equal(reservation.Id, conflict.Id);
        Assert.Equal(reservation.ExpectedEndTime, conflict.End);
    }

    [Fact]
    public async Task UnexpiredPendingPaymentReservation_UsesClaimEndTimeAsConflictEnd()
    {
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var start = WindowStart.AddHours(1);
        var end = WindowStart.AddHours(2);
        var claimEnd = WindowStart.AddHours(3);
        const string policies = """{"Values":{"OCCUPANCY_VALIDITY_TIMESPAN":30,"ALLOCATION_LEAD_TIME":15}}""";
        var reservation = ReservationRow.Prepare(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            tenantId, siteId, Guid.NewGuid(), Guid.NewGuid(), "terms-v1", "PAYOS", start, end, claimEnd, Now,
            WindowStart, 20_000m, 5_000m, "VND", policies, "{}");
        await using var db = await CreateDbAsync(reservation);

        var conflicts = await ConflictsAsync(db, tenantId, siteId, WindowStart, WindowEnd, Now);

        var conflict = Assert.Single(conflicts);
        Assert.Equal(reservation.Id, conflict.Id);
        Assert.Equal("RESERVATION", conflict.Type);
        Assert.Equal(claimEnd, conflict.End);
    }

    [Fact]
    public async Task ExpiredPendingPaymentReservation_IsNotAConflict()
    {
        var (tenantId, siteId) = (Guid.NewGuid(), Guid.NewGuid());
        var reservation = Reservation(tenantId, siteId, WindowStart.AddHours(1), WindowEnd,
            status: "PENDING_PAYMENT", holdExpiresAt: Now.AddMinutes(-30));
        await using var db = await CreateDbAsync(reservation);

        var conflicts = await ConflictsAsync(db, tenantId, siteId, WindowStart, WindowEnd, Now);

        Assert.Empty(conflicts);
    }

    [Fact]
    public async Task UnitScopedWindow_SessionConflict_ReportsNullUnitId()
    {
        var (tenantId, siteId) = (Guid.NewGuid(), Guid.NewGuid());
        var (unitX, scopedSlot) = (Guid.NewGuid(), Guid.NewGuid());
        var session = new ParkingSessionRow
        {
            Id = Guid.NewGuid(), TenantId = tenantId, SiteId = siteId, CurrentSlotId = scopedSlot,
            EntryTime = WindowStart.AddHours(-1), CreatedAt = WindowStart.AddHours(-2), SessionStatus = "ACTIVE"
        };
        await using var db = await CreateDbAsync(session);

        var conflicts = await ConflictsAsync(db, tenantId, siteId, WindowStart, WindowEnd, Now,
            unitId: unitX, scopedSlotIds: new HashSet<Guid> { scopedSlot });

        var conflict = Assert.Single(conflicts);
        Assert.Equal("SESSION", conflict.Type);
        Assert.Equal(session.Id, conflict.Id);
        Assert.Null(conflict.UnitId);
        Assert.Equal(scopedSlot, conflict.SlotId);
    }

    // ---- C. Scope --------------------------------------------------------------------------

    [Fact]
    public async Task SlotScopedWindow_MatchesOnlyTheScopedSlot()
    {
        var (tenantId, siteId) = (Guid.NewGuid(), Guid.NewGuid());
        var (slotA, slotB) = (Guid.NewGuid(), Guid.NewGuid());
        var reservationA = Reservation(tenantId, siteId, WindowStart.AddHours(1), WindowEnd, targetSlotId: slotA);
        var reservationB = Reservation(tenantId, siteId, WindowStart.AddHours(1), WindowEnd, targetSlotId: slotB);
        await using var db = await CreateDbAsync(reservationA, reservationB);

        var forSlotA = await ConflictsAsync(db, tenantId, siteId, WindowStart, WindowEnd, Now, slotId: slotA);
        var forSlotB = await ConflictsAsync(db, tenantId, siteId, WindowStart, WindowEnd, Now, slotId: slotB);

        var matchA = Assert.Single(forSlotA);
        Assert.Equal(reservationA.Id, matchA.Id);
        Assert.Equal(slotA, matchA.SlotId);
        var matchB = Assert.Single(forSlotB);
        Assert.Equal(reservationB.Id, matchB.Id);
        Assert.Equal(slotB, matchB.SlotId);
    }

    [Fact]
    public async Task UnitScopedWindow_MatchesReservationViaScopedSlotIds()
    {
        var (tenantId, siteId) = (Guid.NewGuid(), Guid.NewGuid());
        var (unitX, unitOther, scopedSlot) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var matching = Reservation(tenantId, siteId, WindowStart.AddHours(1), WindowEnd,
            unitId: unitOther, targetSlotId: scopedSlot);
        var unrelated = Reservation(tenantId, siteId, WindowStart.AddHours(1), WindowEnd,
            unitId: unitOther, targetSlotId: Guid.NewGuid());
        await using var db = await CreateDbAsync(matching, unrelated);

        var conflicts = await ConflictsAsync(db, tenantId, siteId, WindowStart, WindowEnd, Now,
            unitId: unitX, scopedSlotIds: new HashSet<Guid> { scopedSlot });

        var conflict = Assert.Single(conflicts);
        Assert.Equal(matching.Id, conflict.Id);
        Assert.Equal(scopedSlot, conflict.SlotId);
    }

    [Fact]
    public async Task UnitScopedWindow_MatchesReservationViaScopedUnitIds()
    {
        var (tenantId, siteId) = (Guid.NewGuid(), Guid.NewGuid());
        var (unitX, unitZ) = (Guid.NewGuid(), Guid.NewGuid());
        var matching = Reservation(tenantId, siteId, WindowStart.AddHours(1), WindowEnd,
            unitId: unitZ, targetSlotId: Guid.NewGuid());
        var unrelated = Reservation(tenantId, siteId, WindowStart.AddHours(1), WindowEnd,
            unitId: Guid.NewGuid(), targetSlotId: Guid.NewGuid());
        await using var db = await CreateDbAsync(matching, unrelated);

        var conflicts = await ConflictsAsync(db, tenantId, siteId, WindowStart, WindowEnd, Now,
            unitId: unitX, scopedUnitIds: new HashSet<Guid> { unitZ });

        var conflict = Assert.Single(conflicts);
        Assert.Equal(matching.Id, conflict.Id);
        Assert.Equal(unitZ, conflict.UnitId);
    }

    [Fact]
    public async Task ReservationEffectiveSlot_UsesLatestActiveAllocation()
    {
        var (tenantId, siteId) = (Guid.NewGuid(), Guid.NewGuid());
        var (olderSlot, newerSlot) = (Guid.NewGuid(), Guid.NewGuid());
        var reservation = Reservation(tenantId, siteId, WindowStart.AddHours(1), WindowEnd);
        await using var db = await CreateDbAsync(reservation,
            Allocation(reservation, olderSlot, "RESERVED", Now.AddHours(-2)),
            Allocation(reservation, newerSlot, "OCCUPIED", Now.AddHours(-1)));

        var forNewer = await ConflictsAsync(db, tenantId, siteId, WindowStart, WindowEnd, Now, slotId: newerSlot);
        var forOlder = await ConflictsAsync(db, tenantId, siteId, WindowStart, WindowEnd, Now, slotId: olderSlot);

        Assert.Equal(newerSlot, Assert.Single(forNewer).SlotId);
        Assert.Empty(forOlder);
    }

    // ---- D. Apply handler -----------------------------------------------------------------

    [Fact]
    public async Task FirstApply_InsertsClosureAndReturnsAcknowledgement()
    {
        var repo = new FakeMaintenanceRepository();
        var work = new StubUnitOfWork();
        var guards = new StubOperationsConcurrency();
        var clock = new MutableTimeProvider(Now);
        var (tenantId, siteId, windowId) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var handler = new ApplyMaintenanceWindowCommandHandler(repo, guards, work, clock);

        var ack = await handler.Handle(new ApplyMaintenanceWindowCommand(tenantId, siteId, windowId,
            null, null, null, null, WindowStart, WindowEnd, "scheduled maintenance"), Ct);

        Assert.NotEqual(Guid.Empty, ack.EnforcementRef);
        Assert.Equal(Now, ack.AcknowledgedAt);
        var stored = repo.FindStored(windowId);
        Assert.NotNull(stored);
        Assert.Equal(tenantId, stored!.TenantId);
        Assert.Equal(siteId, stored.SiteId);
        Assert.Equal(WindowStart, stored.WindowStart);
        Assert.Equal(WindowEnd, stored.WindowEnd);
        Assert.Equal("scheduled maintenance", stored.Reason);
        Assert.Equal(ack.EnforcementRef, stored.EnforcementRef);
        Assert.Equal(ack.AcknowledgedAt, stored.AcknowledgedAt);
        Assert.Null(stored.CancelledAt);
        Assert.Null(stored.ScopedUnitIds);
        Assert.Null(stored.ScopedSlotIds);
        Assert.Equal(1, repo.AddMaintenanceClosureCalls);
        Assert.Equal(1, work.SaveChangesCalls);
        Assert.Equal(1, work.Transaction.CommitCalls);
        Assert.Equal(1, guards.LockSiteCalls);
    }

    [Fact]
    public async Task IdenticalReplay_ReturnsSameAcknowledgementAndDoesNotInsert()
    {
        var repo = new FakeMaintenanceRepository();
        var work = new StubUnitOfWork();
        var guards = new StubOperationsConcurrency();
        var clock = new MutableTimeProvider(Now.AddDays(1));
        var (tenantId, siteId, windowId) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var originalRef = Guid.NewGuid();
        repo.Seed(new StructureMaintenanceClosureRow
        {
            WindowId = windowId, TenantId = tenantId, SiteId = siteId,
            WindowStart = WindowStart, WindowEnd = WindowEnd, Reason = "scheduled maintenance",
            EnforcementRef = originalRef, AcknowledgedAt = Now
        });
        var handler = new ApplyMaintenanceWindowCommandHandler(repo, guards, work, clock);

        var ack = await handler.Handle(new ApplyMaintenanceWindowCommand(tenantId, siteId, windowId,
            null, null, null, null, WindowStart, WindowEnd, "scheduled maintenance"), Ct);

        Assert.Equal(originalRef, ack.EnforcementRef);
        Assert.Equal(Now, ack.AcknowledgedAt);
        Assert.Equal(0, repo.AddMaintenanceClosureCalls);
        Assert.Equal(0, work.SaveChangesCalls);
        Assert.Equal(0, work.Transaction.CommitCalls);
    }

    [Fact]
    public async Task Extension_UpdatesWindowEndWithFreshAcknowledgement()
    {
        var repo = new FakeMaintenanceRepository();
        var work = new StubUnitOfWork();
        var guards = new StubOperationsConcurrency();
        var clock = new MutableTimeProvider(Now.AddDays(1));
        var (tenantId, siteId, windowId) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var originalRef = Guid.NewGuid();
        var extendedEnd = WindowEnd.AddHours(2);
        repo.Seed(new StructureMaintenanceClosureRow
        {
            WindowId = windowId, TenantId = tenantId, SiteId = siteId,
            WindowStart = WindowStart, WindowEnd = WindowEnd, Reason = "old",
            EnforcementRef = originalRef, AcknowledgedAt = Now
        });
        var handler = new ApplyMaintenanceWindowCommandHandler(repo, guards, work, clock);

        var ack = await handler.Handle(new ApplyMaintenanceWindowCommand(tenantId, siteId, windowId,
            null, null, null, null, WindowStart, extendedEnd, "extended"), Ct);

        var stored = repo.FindStored(windowId);
        Assert.NotNull(stored);
        Assert.Equal(extendedEnd, stored!.WindowEnd);
        Assert.Equal("extended", stored.Reason);
        Assert.Equal(ack.EnforcementRef, stored.EnforcementRef);
        Assert.NotEqual(originalRef, ack.EnforcementRef);
        Assert.Equal(Now.AddDays(1), ack.AcknowledgedAt);
        Assert.Equal(0, repo.AddMaintenanceClosureCalls);
        Assert.Equal(1, work.SaveChangesCalls);
        Assert.Equal(1, work.Transaction.CommitCalls);
    }

    [Fact]
    public async Task Extension_WithAdditionalScopedSlots_UpdatesSnapshot()
    {
        var repo = new FakeMaintenanceRepository();
        var work = new StubUnitOfWork();
        var clock = new MutableTimeProvider(Now.AddDays(1));
        var (tenantId, siteId, windowId, unitId) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var (slotA, slotB) = (Guid.NewGuid(), Guid.NewGuid());
        var extendedEnd = WindowEnd.AddHours(1);
        repo.Seed(new StructureMaintenanceClosureRow
        {
            WindowId = windowId, TenantId = tenantId, SiteId = siteId, UnitId = unitId,
            ScopedSlotIds = JsonSerializer.Serialize(new[] { slotA }),
            WindowStart = WindowStart, WindowEnd = WindowEnd, Reason = "old",
            EnforcementRef = Guid.NewGuid(), AcknowledgedAt = Now
        });
        var handler = new ApplyMaintenanceWindowCommandHandler(repo, new StubOperationsConcurrency(), work, clock);

        var ack = await handler.Handle(new ApplyMaintenanceWindowCommand(tenantId, siteId, windowId,
            unitId, null, null, [slotA, slotB], WindowStart, extendedEnd, "extended"), Ct);

        var stored = repo.FindStored(windowId);
        Assert.NotNull(stored);
        Assert.Equal(extendedEnd, stored!.WindowEnd);
        Assert.Equal(Now.AddDays(1), ack.AcknowledgedAt);
        Assert.Equal(ack.EnforcementRef, stored.EnforcementRef);
        var scopedSlots = JsonSerializer.Deserialize<Guid[]>(stored.ScopedSlotIds!)!;
        Assert.Equal(new[] { slotA, slotB }.OrderBy(x => x), scopedSlots.OrderBy(x => x));
        Assert.Equal(0, repo.AddMaintenanceClosureCalls);
        Assert.Equal(1, work.SaveChangesCalls);
        Assert.Equal(1, work.Transaction.CommitCalls);
    }

    [Fact]
    public async Task ShortenedEnd_IsRejected()
    {
        var repo = new FakeMaintenanceRepository();
        var work = new StubUnitOfWork();
        var (tenantId, siteId, windowId) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        repo.Seed(new StructureMaintenanceClosureRow
        {
            WindowId = windowId, TenantId = tenantId, SiteId = siteId,
            WindowStart = WindowStart, WindowEnd = WindowEnd, Reason = "old",
            EnforcementRef = Guid.NewGuid(), AcknowledgedAt = Now
        });
        var handler = new ApplyMaintenanceWindowCommandHandler(repo, new StubOperationsConcurrency(), work, new MutableTimeProvider(Now));

        await Assert.ThrowsAsync<InvalidStructureEditException>(() => handler.Handle(
            new ApplyMaintenanceWindowCommand(tenantId, siteId, windowId, null, null, null, null,
                WindowStart, WindowEnd.AddMinutes(-30), "shorter"), Ct));

        Assert.Equal(0, work.SaveChangesCalls);
    }

    [Fact]
    public async Task MovedStart_IsRejected()
    {
        var repo = new FakeMaintenanceRepository();
        var work = new StubUnitOfWork();
        var (tenantId, siteId, windowId) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        repo.Seed(new StructureMaintenanceClosureRow
        {
            WindowId = windowId, TenantId = tenantId, SiteId = siteId,
            WindowStart = WindowStart, WindowEnd = WindowEnd, Reason = "old",
            EnforcementRef = Guid.NewGuid(), AcknowledgedAt = Now
        });
        var handler = new ApplyMaintenanceWindowCommandHandler(repo, new StubOperationsConcurrency(), work, new MutableTimeProvider(Now));

        await Assert.ThrowsAsync<InvalidStructureEditException>(() => handler.Handle(
            new ApplyMaintenanceWindowCommand(tenantId, siteId, windowId, null, null, null, null,
                WindowStart.AddHours(1), WindowEnd.AddHours(1), "moved"), Ct));

        Assert.Equal(0, work.SaveChangesCalls);
    }

    [Fact]
    public async Task ScopeChange_IsRejected()
    {
        var repo = new FakeMaintenanceRepository();
        var work = new StubUnitOfWork();
        var (tenantId, siteId, windowId) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        repo.Seed(new StructureMaintenanceClosureRow
        {
            WindowId = windowId, TenantId = tenantId, SiteId = siteId,
            WindowStart = WindowStart, WindowEnd = WindowEnd, Reason = "old",
            EnforcementRef = Guid.NewGuid(), AcknowledgedAt = Now
        });
        var handler = new ApplyMaintenanceWindowCommandHandler(repo, new StubOperationsConcurrency(), work, new MutableTimeProvider(Now));

        await Assert.ThrowsAsync<InvalidStructureEditException>(() => handler.Handle(
            new ApplyMaintenanceWindowCommand(tenantId, siteId, windowId, Guid.NewGuid(), null, null, null,
                WindowStart, WindowEnd, "rescoped"), Ct));

        Assert.Equal(0, work.SaveChangesCalls);
    }

    [Fact]
    public async Task CancelledWindow_CannotBeReapplied()
    {
        var repo = new FakeMaintenanceRepository();
        var work = new StubUnitOfWork();
        var (tenantId, siteId, windowId) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        repo.Seed(new StructureMaintenanceClosureRow
        {
            WindowId = windowId, TenantId = tenantId, SiteId = siteId,
            WindowStart = WindowStart, WindowEnd = WindowEnd, Reason = "cancelled",
            EnforcementRef = Guid.NewGuid(), AcknowledgedAt = Now, CancelledAt = Now.AddMinutes(5)
        });
        var handler = new ApplyMaintenanceWindowCommandHandler(repo, new StubOperationsConcurrency(), work, new MutableTimeProvider(Now));

        var error = await Assert.ThrowsAsync<InvalidStructureEditException>(() => handler.Handle(
            new ApplyMaintenanceWindowCommand(tenantId, siteId, windowId, null, null, null, null,
                WindowStart, WindowEnd, "apply again"), Ct));

        Assert.Equal("Cancelled maintenance window cannot be re-applied.", error.Message);
        Assert.Equal(0, work.SaveChangesCalls);
    }

    [Fact]
    public async Task OverlappingCommitment_ThrowsCommitmentConflict()
    {
        var repo = new FakeMaintenanceRepository();
        var work = new StubUnitOfWork();
        var (tenantId, siteId, windowId) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var conflict = new MaintenanceCommitmentConflict(Guid.NewGuid(), "RESERVATION",
            WindowStart.AddHours(1), WindowEnd, Guid.NewGuid(), Guid.NewGuid());
        repo.Conflicts = [conflict];
        var handler = new ApplyMaintenanceWindowCommandHandler(repo, new StubOperationsConcurrency(), work, new MutableTimeProvider(Now));

        var error = await Assert.ThrowsAsync<MaintenanceCommitmentConflictException>(() => handler.Handle(
            new ApplyMaintenanceWindowCommand(tenantId, siteId, windowId, null, null, null, null,
                WindowStart, WindowEnd, "scheduled maintenance"), Ct));

        var reported = Assert.Single(error.Commitments);
        Assert.Equal(conflict, reported);
        Assert.Equal("1 commitment(s) overlap the requested maintenance window.", error.Message);
        Assert.Equal(0, repo.AddMaintenanceClosureCalls);
        Assert.Equal(0, work.SaveChangesCalls);
        Assert.Null(repo.FindStored(windowId));
    }

    // ---- E. Cancel handler ----------------------------------------------------------------

    [Fact]
    public async Task Cancel_SetsCancelledAtOnce()
    {
        var repo = new FakeMaintenanceRepository();
        var work = new StubUnitOfWork();
        var clock = new MutableTimeProvider(Now);
        var (tenantId, siteId, windowId) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        repo.Seed(new StructureMaintenanceClosureRow
        {
            WindowId = windowId, TenantId = tenantId, SiteId = siteId,
            WindowStart = WindowStart, WindowEnd = WindowEnd, Reason = "scheduled maintenance",
            EnforcementRef = Guid.NewGuid(), AcknowledgedAt = Now.AddDays(-1)
        });
        var handler = new CancelMaintenanceWindowCommandHandler(repo, new StubOperationsConcurrency(), work, clock);

        await handler.Handle(new CancelMaintenanceWindowCommand(tenantId, siteId, windowId), Ct);

        Assert.Equal(Now, repo.FindStored(windowId)!.CancelledAt);
        Assert.Equal(1, work.SaveChangesCalls);
        Assert.Equal(1, work.Transaction.CommitCalls);
    }

    [Fact]
    public async Task SecondCancel_IsIdempotent()
    {
        var repo = new FakeMaintenanceRepository();
        var work = new StubUnitOfWork();
        var clock = new MutableTimeProvider(Now);
        var (tenantId, siteId, windowId) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        repo.Seed(new StructureMaintenanceClosureRow
        {
            WindowId = windowId, TenantId = tenantId, SiteId = siteId,
            WindowStart = WindowStart, WindowEnd = WindowEnd, Reason = "scheduled maintenance",
            EnforcementRef = Guid.NewGuid(), AcknowledgedAt = Now.AddDays(-1)
        });
        var handler = new CancelMaintenanceWindowCommandHandler(repo, new StubOperationsConcurrency(), work, clock);

        await handler.Handle(new CancelMaintenanceWindowCommand(tenantId, siteId, windowId), Ct);
        var firstCancelledAt = repo.FindStored(windowId)!.CancelledAt;
        clock.UtcNow = Now.AddHours(3);
        await handler.Handle(new CancelMaintenanceWindowCommand(tenantId, siteId, windowId), Ct);

        Assert.Equal(Now, firstCancelledAt);
        Assert.Equal(firstCancelledAt, repo.FindStored(windowId)!.CancelledAt);
        Assert.Equal(1, work.SaveChangesCalls);
        Assert.Equal(2, work.Transaction.CommitCalls);
    }

    [Fact]
    public async Task CancelMissingWindow_IsNoOp()
    {
        var repo = new FakeMaintenanceRepository();
        var work = new StubUnitOfWork();
        var handler = new CancelMaintenanceWindowCommandHandler(repo, new StubOperationsConcurrency(), work, new MutableTimeProvider(Now));

        await handler.Handle(new CancelMaintenanceWindowCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), Ct);

        Assert.Equal(0, work.SaveChangesCalls);
        Assert.Equal(0, work.Transaction.CommitCalls);
        Assert.True(work.Transaction.Disposed);
    }

    // ---- G. Exception handler -------------------------------------------------------------

    [Fact]
    public async Task ExceptionHandler_MapsCommitmentConflictToStructureInUseWithCommitments()
    {
        var conflict = new MaintenanceCommitmentConflict(Guid.NewGuid(), "RESERVATION",
            WindowStart, WindowEnd, Guid.NewGuid(), Guid.NewGuid());
        var handler = new ReservationExceptionHandler(NullLogger<ReservationExceptionHandler>.Instance);
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        var handled = await handler.TryHandleAsync(context,
            new MaintenanceCommitmentConflictException([conflict],
                "1 commitment(s) overlap the requested maintenance window."), Ct);

        Assert.True(handled);
        Assert.Equal(409, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body, cancellationToken: Ct);
        var root = document.RootElement;
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("STRUCTURE_IN_USE", root.GetProperty("code").GetString());
        Assert.Equal("1 commitment(s) overlap the requested maintenance window.", root.GetProperty("message").GetString());
        Assert.False(string.IsNullOrEmpty(root.GetProperty("traceId").GetString()));
        var item = Assert.Single(root.GetProperty("commitments").EnumerateArray());
        Assert.Equal(conflict.Id, item.GetProperty("id").GetGuid());
        Assert.Equal("RESERVATION", item.GetProperty("type").GetString());
        Assert.Equal(conflict.Start, item.GetProperty("start").GetDateTimeOffset());
        Assert.Equal(conflict.End, item.GetProperty("end").GetDateTimeOffset());
        Assert.Equal(conflict.UnitId, item.GetProperty("unitId").GetGuid());
        Assert.Equal(conflict.SlotId, item.GetProperty("slotId").GetGuid());
    }

    // ---- helpers --------------------------------------------------------------------------

    private static Task<IReadOnlyList<MaintenanceCommitmentConflict>> ConflictsAsync(ReservationDbContext db,
        Guid tenantId, Guid siteId, DateTimeOffset start, DateTimeOffset end, DateTimeOffset now,
        Guid? unitId = null, Guid? slotId = null,
        IReadOnlySet<Guid>? scopedUnitIds = null, IReadOnlySet<Guid>? scopedSlotIds = null) =>
        new StructureRepository(db).FindMaintenanceConflictsAsync(tenantId, siteId, unitId, slotId,
            scopedUnitIds ?? new HashSet<Guid>(), scopedSlotIds ?? new HashSet<Guid>(), start, end, now, Ct);

    private static ReservationRow Reservation(Guid tenantId, Guid siteId, DateTimeOffset start, DateTimeOffset end,
        Guid? unitId = null, Guid? targetSlotId = null, Guid? activeClaimSlotId = null, string status = "CONFIRMED",
        DateTimeOffset? holdExpiresAt = null) =>
        new()
        {
            Id = Guid.NewGuid(), TenantId = tenantId, SiteId = siteId, AccountId = Guid.NewGuid(),
            ExpectedStartTime = start, ExpectedEndTime = end, TargetSpatialUnitId = unitId,
            TargetSlotId = targetSlotId, ActiveClaimSlotId = activeClaimSlotId, HoldExpiresAt = holdExpiresAt,
            VehicleType = "CAR", Status = status, CreatedAt = start.AddDays(-1)
        };

    private static SlotAllocationRow Allocation(ReservationRow reservation, Guid slotId, string status,
        DateTimeOffset createdAt) =>
        new()
        {
            Id = Guid.NewGuid(), ReservationId = reservation.Id, TenantId = reservation.TenantId,
            SiteId = reservation.SiteId, SlotId = slotId, AllocationStatus = status, CreatedAt = createdAt,
            AllocatedStartTime = reservation.ExpectedStartTime, AllocatedEndTime = reservation.ExpectedEndTime
        };

    private static async Task<ReservationDbContext> CreateDbAsync(params object[] rows)
    {
        var options = new DbContextOptionsBuilder<ReservationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new ReservationDbContext(options);
        db.AddRange(rows);
        await db.SaveChangesAsync(Ct);
        db.ChangeTracker.Clear();
        return db;
    }
}
