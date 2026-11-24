using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Domain.Reservations;
using SmartParking.OperationService.Persistence;
using SmartParking.OperationService.Persistence.Repositories;
using SmartParking.OperationService.Tests.TestDoubles;

namespace SmartParking.OperationService.Tests;

// Slice 3B / P3.5: Facility capacity restrictions must protect parked vehicles, reallocate where a
// verified free bay exists, and otherwise cancel with FACILITY_FAILURE evidence plus a refund.
public sealed class EmergencyRestrictionReconciliationTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 10, 0, 0, TimeSpan.Zero);
    private static readonly CancellationToken Ct = CancellationToken.None;
    private const string Policies =
        """{"Values":{"OCCUPANCY_VALIDITY_TIMESPAN":30,"ALLOCATION_LEAD_TIME":15}}""";

    [Fact]
    public async Task ParkedVehicle_IsNeverCancelledOrReallocated()
    {
        await using var harness = new Harness();
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var parked = ConfirmedBooking(tenantId, siteId, slotId);
        parked.EnterParking();
        var allocation = Allocation(parked, slotId, "OCCUPIED");
        await harness.SeedAsync(parked, allocation, Pool(tenantId, siteId, reserved: 1, pending: 0));
        harness.Slots.Candidates = [Candidate(slotId, null)];

        await harness.Handler().Handle(Restriction(tenantId, siteId, "SITE", null, []), Ct);

        var stored = await harness.Db.Reservations.SingleAsync();
        Assert.Equal("PARKING", stored.Status);
        Assert.Equal(slotId, stored.TargetSlotId);
        Assert.Null(stored.CancelledAt);
        var storedAllocation = await harness.Db.SlotAllocations.SingleAsync();
        Assert.Equal("OCCUPIED", storedAllocation.AllocationStatus);
        Assert.Equal(slotId, storedAllocation.SlotId);
        Assert.Empty(harness.Commercial.CollectionCalls);
        Assert.Empty(harness.Commercial.ReconcileCalls);
        Assert.Empty(await harness.Db.Set<ReservationCancellationEvidence>().ToListAsync());
    }

    [Fact]
    public async Task SlotScopedRestriction_MovesTheBookingToAVerifiedFreeBay()
    {
        await using var harness = new Harness();
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var closedSlot = Guid.NewGuid();
        var freeSlot = Guid.NewGuid();
        var booking = ConfirmedBooking(tenantId, siteId, closedSlot);
        booking.TargetSpatialUnitId = unitId;
        var allocation = Allocation(booking, closedSlot, "RESERVED");
        await harness.SeedAsync(booking, allocation, Pool(tenantId, siteId, reserved: 1, pending: 0));
        harness.Slots.Candidates =
        [
            Candidate(closedSlot, unitId),
            Candidate(freeSlot, unitId)
        ];

        await harness.Handler().Handle(Restriction(tenantId, siteId, "SLOT", null, [closedSlot]), Ct);

        var stored = await harness.Db.Reservations.SingleAsync();
        Assert.Equal("CONFIRMED", stored.Status);
        Assert.Equal(freeSlot, stored.TargetSlotId);
        Assert.Equal(freeSlot, stored.ActiveClaimSlotId);
        Assert.Null(stored.CancelledAt);
        var storedAllocation = await harness.Db.SlotAllocations.SingleAsync();
        Assert.Equal("RESERVED", storedAllocation.AllocationStatus);
        Assert.Equal(freeSlot, storedAllocation.SlotId);
        Assert.Empty(harness.Commercial.ReconcileCalls);
        Assert.Empty(await harness.Db.Set<ReservationCancellationEvidence>().ToListAsync());
        Assert.Equal(1, (await harness.Db.SiteCapacityPools.SingleAsync()).CurrentReservedCount);
    }

    [Fact]
    public async Task SiteWideRestriction_CancelsThePaidBookingAndRequestsTheRefund()
    {
        await using var harness = new Harness();
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var booking = ConfirmedBooking(tenantId, siteId, slotId);
        var allocation = Allocation(booking, slotId, "RESERVED");
        await harness.SeedAsync(booking, allocation, Pool(tenantId, siteId, reserved: 1, pending: 0));
        harness.Commercial.Collections = [Collection(booking)];
        var command = Restriction(tenantId, siteId, "SITE", null, []);

        await harness.Handler().Handle(command, Ct);

        var stored = await harness.Db.Reservations.SingleAsync();
        Assert.Equal("CANCELLED", stored.Status);
        Assert.NotNull(stored.CancelledAt);
        Assert.Null(stored.ActiveClaimSlotId);
        Assert.Equal("NO_ENTITLEMENT", stored.ConfirmationState);
        Assert.Equal("FACILITY_FAILURE_RECONCILED", stored.ConfirmationCode);

        var evidence = await harness.Db.Set<ReservationCancellationEvidence>().SingleAsync();
        Assert.Equal(booking.Id, evidence.ReservationId);
        Assert.Equal("FACILITY_FAILURE", evidence.Reason);
        Assert.Null(evidence.RestrictedUntil);

        Assert.Equal("RELEASED", (await harness.Db.SlotAllocations.SingleAsync()).AllocationStatus);
        Assert.Equal(0, (await harness.Db.SiteCapacityPools.SingleAsync()).CurrentReservedCount);

        var call = Assert.Single(harness.Commercial.ReconcileCalls);
        Assert.Equal($"FACILITY_FAILURE:{command.RestrictionId:D}", call.Reason);
        Assert.Single(harness.Commercial.CollectionCalls);
        Assert.Equal(0, harness.Slots.ListCalls);
    }

    [Fact]
    public async Task UnpaidHold_IsCancelledWithoutAnyPaymentCall()
    {
        await using var harness = new Harness();
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var booking = PendingBooking(tenantId, siteId, slotId);
        await harness.SeedAsync(booking, Pool(tenantId, siteId, reserved: 0, pending: 1));
        var command = Restriction(tenantId, siteId, "SITE", null, []);

        await harness.Handler().Handle(command, Ct);

        var stored = await harness.Db.Reservations.SingleAsync();
        Assert.Equal("CANCELLED", stored.Status);
        Assert.Equal("NO_ENTITLEMENT", stored.ConfirmationState);
        Assert.Equal($"FACILITY_FAILURE:{command.RestrictionId:D}", stored.ConfirmationCode);
        Assert.Single(await harness.Db.Set<ReservationCancellationEvidence>().ToListAsync());
        Assert.Equal(0, (await harness.Db.SiteCapacityPools.SingleAsync()).PendingPaymentCount);
        Assert.Empty(harness.Commercial.CollectionCalls);
        Assert.Empty(harness.Commercial.ReconcileCalls);
    }

    [Fact]
    public async Task DuplicateEventId_IsFullyIdempotent()
    {
        await using var harness = new Harness();
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var booking = ConfirmedBooking(tenantId, siteId, slotId);
        await harness.SeedAsync(booking, Allocation(booking, slotId, "RESERVED"),
            Pool(tenantId, siteId, reserved: 1, pending: 0));
        harness.Commercial.Collections = [Collection(booking)];
        var command = Restriction(tenantId, siteId, "SITE", null, []);

        await harness.Handler().Handle(command, Ct);
        var poolAfterFirst = (await harness.Db.SiteCapacityPools.SingleAsync()).CurrentReservedCount;
        await harness.Handler().Handle(command, Ct);

        Assert.Single(harness.Commercial.CollectionCalls);
        Assert.Single(harness.Commercial.ReconcileCalls);
        Assert.Single(await harness.Db.Set<ReservationCancellationEvidence>().ToListAsync());
        Assert.Single(await harness.Db.StructureEmergencyClosures.ToListAsync());
        Assert.Single(await harness.Db.FacilityEventReceipts.ToListAsync());
        Assert.Equal(poolAfterFirst, (await harness.Db.SiteCapacityPools.SingleAsync()).CurrentReservedCount);
        Assert.Equal(0, poolAfterFirst);
    }

    [Fact]
    public async Task TransientRefundFailure_KeepsReconciliationPendingAndSucceedsOnRetry()
    {
        await using var harness = new Harness();
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var booking = ConfirmedBooking(tenantId, siteId, slotId);
        await harness.SeedAsync(booking, Allocation(booking, slotId, "RESERVED"),
            Pool(tenantId, siteId, reserved: 1, pending: 0));
        harness.Commercial.Collections = [Collection(booking)];
        var attempts = 0;
        harness.Commercial.ReconcileFailure = _ => ++attempts == 1
            ? new ReservationGateException(503, "OWNER_UNAVAILABLE", "Payment service is unavailable.")
            : null;
        var command = Restriction(tenantId, siteId, "SITE", null, []);

        var error = await Assert.ThrowsAsync<ReservationGateException>(
            () => harness.Handler().Handle(command, Ct));

        Assert.Equal("OWNER_UNAVAILABLE", error.Code);
        var cancelled = await harness.Db.Reservations.SingleAsync();
        Assert.Equal("CANCELLED", cancelled.Status);
        Assert.Equal("RECONCILIATION_REQUIRED", cancelled.ConfirmationState);
        Assert.Empty(await harness.Db.FacilityEventReceipts.ToListAsync());

        await harness.Handler().Handle(command, Ct);

        var reconciled = await harness.Db.Reservations.SingleAsync();
        Assert.Equal("NO_ENTITLEMENT", reconciled.ConfirmationState);
        Assert.Equal("FACILITY_FAILURE_RECONCILED", reconciled.ConfirmationCode);
        Assert.Single(await harness.Db.FacilityEventReceipts.ToListAsync());
        Assert.Single(await harness.Db.Set<ReservationCancellationEvidence>().ToListAsync());
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task Release_MarksTheClosureReleasedAndIsIdempotent()
    {
        await using var harness = new Harness();
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var restrictionId = Guid.NewGuid();
        harness.Db.StructureEmergencyClosures.Add(new StructureEmergencyClosureRow
        {
            RestrictionId = restrictionId, TenantId = tenantId, SiteId = siteId, Mode = "EMERGENCY",
            Scope = "SITE", AffectedSlotIds = "[]", Reason = "structural collapse",
            EffectiveFrom = Now, ReceivedAt = Now
        });
        await harness.Db.SaveChangesAsync();
        harness.Db.ChangeTracker.Clear();
        var command = new ReleaseFacilityCapacityRestrictionCommand(
            Guid.NewGuid(), 1, tenantId, siteId, restrictionId, []);
        var handler = new ReleaseFacilityCapacityRestrictionCommandHandler(harness.Store, harness.Clock);

        await handler.Handle(command, Ct);
        var releasedAt = (await harness.Db.StructureEmergencyClosures.SingleAsync()).ReleasedAt;
        await handler.Handle(command, Ct);

        Assert.Equal(Now, releasedAt);
        var stored = await harness.Db.StructureEmergencyClosures.SingleAsync();
        Assert.Equal(Now, stored.ReleasedAt);
        Assert.Single(await harness.Db.FacilityEventReceipts.ToListAsync());
    }

    [Fact]
    public async Task SiteWideEmergencyClosure_BlocksAdmission()
    {
        var setup = Admission();

        var error = await Assert.ThrowsAsync<ReservationGateException>(() => setup
            .Handler(Emergency(setup, scope: "SITE")).Handle(setup.Command, Ct));

        Assert.Equal(409, error.StatusCode);
        Assert.Equal("SLOT_UNAVAILABLE", error.Code);
        Assert.Equal("The selected parking area is closed due to an emergency restriction.", error.Message);
    }

    [Fact]
    public async Task UnitScopedEmergencyClosure_BlocksTheReservedArea()
    {
        var unitId = Guid.NewGuid();
        var setup = Admission(proposedUnitId: unitId);

        var error = await Assert.ThrowsAsync<ReservationGateException>(() => setup
            .Handler(Emergency(setup, scope: "UNIT", unitId: unitId)).Handle(setup.Command, Ct));

        Assert.Equal(409, error.StatusCode);
        Assert.Equal("SLOT_UNAVAILABLE", error.Code);
    }

    [Fact]
    public async Task SlotScopedEmergencyClosure_BlocksTheAffectedBay()
    {
        var setup = Admission();

        var error = await Assert.ThrowsAsync<ReservationGateException>(() =>
            setup.Handler(Emergency(setup, scope: "SLOT", affectedSlotIds: [setup.SlotId]))
                .Handle(setup.Command, Ct));

        Assert.Equal(409, error.StatusCode);
        Assert.Equal("SLOT_UNAVAILABLE", error.Code);
    }

    [Fact]
    public async Task ReleasedOrExpiredEmergencyClosure_BlocksNothing()
    {
        var setup = Admission();

        await setup.Handler(Emergency(setup, scope: "SITE", releasedAt: Now)).Handle(setup.Command, Ct);
        await setup.Handler(Emergency(setup, scope: "SITE", effectiveUntil: setup.Start))
            .Handle(setup.Command, Ct);
    }

    // The refund retry after a redelivery must only resume the cancellation this restriction owns;
    // a booking waiting on another workflow or restriction is not ours to reconcile.
    [Fact]
    public async Task PendingRefundOwnedByAnotherRestriction_IsNotHijacked()
    {
        await using var harness = new Harness();
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var booking = ConfirmedBooking(tenantId, siteId, slotId);
        var foreignRestrictionId = Guid.NewGuid();
        booking.CancelForFacilityFailure(Now.AddMinutes(2), requiresPaymentReconciliation: true,
            restrictionId: foreignRestrictionId);
        await harness.SeedAsync(booking, Pool(tenantId, siteId, reserved: 0, pending: 0));
        harness.Commercial.Collections = [Collection(booking)];

        await harness.Handler().Handle(Restriction(tenantId, siteId, "SITE", null, []), Ct);

        var stored = await harness.Db.Reservations.SingleAsync();
        Assert.Equal("CANCELLED", stored.Status);
        Assert.Equal("RECONCILIATION_REQUIRED", stored.ConfirmationState);
        Assert.Equal($"FACILITY_FAILURE:{foreignRestrictionId:D}", stored.ConfirmationCode);
        Assert.Empty(harness.Commercial.CollectionCalls);
        Assert.Empty(harness.Commercial.ReconcileCalls);
    }

    // A bay already promised to another customer through a live hold is not free capacity and must
    // never receive a reallocated booking.
    [Fact]
    public async Task LivePendingHold_PreventsReallocationToThatBay()
    {
        await using var harness = new Harness();
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var closedSlot = Guid.NewGuid();
        var heldSlot = Guid.NewGuid();
        var freeSlot = Guid.NewGuid();
        var booking = ConfirmedBooking(tenantId, siteId, closedSlot);
        var held = PendingBooking(tenantId, siteId, heldSlot);
        await harness.SeedAsync(booking, Allocation(booking, closedSlot, "RESERVED"), held,
            Pool(tenantId, siteId, reserved: 1, pending: 1));
        harness.Slots.Candidates = [Candidate(heldSlot, null), Candidate(freeSlot, null)];

        await harness.Handler().Handle(Restriction(tenantId, siteId, "SLOT", null, [closedSlot]), Ct);

        var stored = await harness.Db.Reservations.SingleAsync(row => row.Id == booking.Id);
        Assert.Equal("CONFIRMED", stored.Status);
        Assert.Equal(freeSlot, stored.TargetSlotId);
        Assert.Null(stored.CancelledAt);
        Assert.Empty(harness.Commercial.ReconcileCalls);
    }

    // An allocated booking may record its bay only through the allocation row, so the restriction
    // must be detected from that record too.
    [Fact]
    public async Task AllocatedBookingWithoutTargetSlot_IsDetectedThroughItsAllocation()
    {
        await using var harness = new Harness();
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var closedSlot = Guid.NewGuid();
        var booking = ConfirmedBooking(tenantId, siteId, closedSlot);
        booking.Status = "ALLOCATED";
        booking.TargetSlotId = null;
        booking.ActiveClaimSlotId = null;
        await harness.SeedAsync(booking, Allocation(booking, closedSlot, "RESERVED"),
            Pool(tenantId, siteId, reserved: 1, pending: 0));

        await harness.Handler().Handle(Restriction(tenantId, siteId, "SLOT", null, [closedSlot]), Ct);

        var stored = await harness.Db.Reservations.SingleAsync();
        Assert.Equal("CANCELLED", stored.Status);
        Assert.NotNull(stored.CancelledAt);
        Assert.Equal("RELEASED", (await harness.Db.SlotAllocations.SingleAsync()).AllocationStatus);
        Assert.Single(await harness.Db.Set<ReservationCancellationEvidence>().ToListAsync());
        Assert.Equal(0, (await harness.Db.SiteCapacityPools.SingleAsync()).CurrentReservedCount);
    }

    private static ReconcileFacilityCapacityRestrictedCommand Restriction(Guid tenantId, Guid siteId,
        string scope, Guid? unitId, IReadOnlyList<Guid> affectedSlotIds, DateTimeOffset? until = null) =>
        new(Guid.NewGuid(), 1, tenantId, siteId, Guid.NewGuid(), "EMERGENCY", scope, unitId,
            affectedSlotIds, Now, until, "structural collapse");

    private static ReservationRow PendingBooking(Guid tenantId, Guid siteId, Guid slotId) =>
        ReservationRow.Prepare(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            tenantId, siteId, slotId, Guid.NewGuid(), "terms-v1", "PAYOS",
            Now.AddHours(2), Now.AddHours(3), Now.AddHours(3), Now, Now.AddMinutes(30),
            20_000m, 5_000m, "VND", Policies, "{}");

    private static ReservationRow ConfirmedBooking(Guid tenantId, Guid siteId, Guid slotId)
    {
        var booking = PendingBooking(tenantId, siteId, slotId);
        booking.RecordOrderCreated(Guid.NewGuid());
        booking.ConfirmAtEvaluation(Guid.NewGuid(), Now.AddMinutes(1));
        return booking;
    }

    private static SlotAllocationRow Allocation(ReservationRow booking, Guid slotId, string status) => new()
    {
        Id = Guid.NewGuid(), ReservationId = booking.Id, TenantId = booking.TenantId,
        SiteId = booking.SiteId, SlotId = slotId, AllocationStatus = status, CreatedAt = Now,
        AllocatedStartTime = booking.ExpectedStartTime, AllocatedEndTime = booking.ExpectedEndTime
    };

    private static SiteCapacityPoolRow Pool(Guid tenantId, Guid siteId, int reserved, int pending) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, SiteId = siteId, SpatialUnitId = null,
        VehicleType = "CAR", TotalCapacity = 10, MaxReservationQuota = 10,
        CurrentReservedCount = reserved, PendingPaymentCount = pending, UpdatedAt = Now
    };

    private static FacilitySlotCandidate Candidate(Guid slotId, Guid? unitId) =>
        new(slotId, unitId, "CAR", Eligible: true, Active: true, Reachable: true, Backup: false);

    private static VerifiedCollection Collection(ReservationRow booking) =>
        new(Guid.NewGuid(), booking.PaymentOrderId!.Value, booking.Id, booking.AccountId, booking.TenantId,
            booking.SiteId, Guid.NewGuid(), booking.TermsVersion!, booking.DepositAmount ?? 0m, "VND");

    private static StructureEmergencyClosureRow Emergency(AdmissionSetup setup, string scope,
        Guid? unitId = null, IReadOnlyList<Guid>? affectedSlotIds = null, DateTimeOffset? releasedAt = null,
        DateTimeOffset? effectiveUntil = null) =>
        new()
        {
            RestrictionId = Guid.NewGuid(), TenantId = setup.TenantId, SiteId = setup.SiteId,
            Mode = "EMERGENCY", Scope = scope, UnitId = unitId,
            AffectedSlotIds = JsonSerializer.Serialize(affectedSlotIds ?? []),
            Reason = "structural collapse", EffectiveFrom = setup.Start.AddMinutes(-30),
            EffectiveUntil = effectiveUntil, ReceivedAt = Now, ReleasedAt = releasedAt
        };

    private static AdmissionSetup Admission(Guid? proposedUnitId = null)
    {
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var start = Now.AddHours(2);
        var end = start.AddHours(1);
        var proposed = ReservationRow.Prepare(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            tenantId, siteId, slotId, Guid.NewGuid(), "terms-v1", "PAYOS", start, end, end, Now,
            Now.AddMinutes(30), 20_000m, 5_000m, "VND", Policies, "{}");
        proposed.TargetSpatialUnitId = proposedUnitId;
        var snapshot = new FacilityResourceSnapshot(
            new FacilityBookingFacts(tenantId, siteId, slotId, "v1", EligibleCarSlot: true,
                ParticipatesInStructureGuard: true),
            [new FacilityResourceFact(slotId, proposedUnitId, "CAR", Active: true, Reachable: true,
                FixedBackup: false)],
            [], Complete: true);
        var quotas = new OperationalCapacityConfig[]
        {
            new()
            {
                Id = Guid.NewGuid(), TenantId = tenantId, SiteId = siteId, SpatialUnitId = null,
                VehicleType = "CAR", TotalPhysicalCapacity = 1, MaxReservationQuota = 1, IsActive = true,
                CreatedAt = Now, UpdatedAt = Now
            }
        };
        return new AdmissionSetup(tenantId, siteId, slotId, proposed, snapshot, quotas, start);
    }

    private sealed record AdmissionSetup(Guid TenantId, Guid SiteId, Guid SlotId, ReservationRow Proposed,
        FacilityResourceSnapshot Snapshot, IReadOnlyList<OperationalCapacityConfig> Quotas, DateTimeOffset Start)
    {
        public RequireReservationAvailabilityCommand Command => new(Proposed, Snapshot, Now);

        public RequireReservationAvailabilityHandler Handler(params StructureEmergencyClosureRow[] closures) =>
            new(new FakeAvailabilityRepository(
                new ReservationAvailabilityFacts([], [], [], Quotas, null, closures)));
    }

    private sealed class FakeAvailabilityRepository(ReservationAvailabilityFacts facts)
        : IReservationAvailabilityRepository
    {
        public Task<ReservationAvailabilityFacts> ReadAsync(ReservationRow proposed, DateTimeOffset evaluatedAt,
            CancellationToken ct) => Task.FromResult(facts);

        public Task LockAccountAsync(Guid accountId, CancellationToken ct) => Task.CompletedTask;

        public Task<bool> HasUnclassifiedCancellationAsync(Guid accountId, CancellationToken ct) =>
            Task.FromResult(false);

        public Task<IReadOnlyList<ReservationCancellationEvidence>> ReadCancellationsAsync(Guid accountId,
            DateTimeOffset dayStart, DateTimeOffset now, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ReservationCancellationEvidence>>([]);
    }

    private sealed class Harness : IAsyncDisposable
    {
        public Harness()
        {
            Db = new ReservationDbContext(new DbContextOptionsBuilder<ReservationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            Bookings = new ReservationRepository(Db);
            Work = new ReconciliationUnitOfWork(Db);
            Store = new EmergencyClosureReconciliationStore(Db, Clock);
        }

        public ReservationDbContext Db { get; }
        public ReservationRepository Bookings { get; }
        public ReconciliationUnitOfWork Work { get; }
        public RecordingSlotCandidateProvider Slots { get; } = new();
        public RecordingCommercialOwner Commercial { get; } = new();
        public RecordingOperationsConcurrency Guards { get; } = new();
        public StubAvailabilityRepository Availability { get; } = new(new ReservationAvailabilityFacts([], [], [], []));
        public MutableTimeProvider Clock { get; } = new(Now);
        public EmergencyClosureReconciliationStore Store { get; }

        public ReconcileFacilityCapacityRestrictedCommandHandler Handler() =>
            new(Store, Bookings, Slots, Availability, Guards, Commercial, Work, Clock);

        public async Task SeedAsync(params object[] rows)
        {
            Db.AddRange(rows);
            await Db.SaveChangesAsync(Ct);
            Db.ChangeTracker.Clear();
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}