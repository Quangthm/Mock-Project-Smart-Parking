using System.Reflection;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Domain.Reservations;
using SmartParking.OperationService.Infrastructure.Messaging;
using SmartParking.OperationService.Persistence;
using SmartParking.OperationService.Persistence.Repositories;
using SmartParking.OperationService.Tests.TestDoubles;

namespace SmartParking.OperationService.Tests;

// Option B / SPARK-273 event-driven maintenance closure projection & reconciliation tests.
public sealed class MaintenanceClosureEnforcementTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset WindowStart = Now.AddHours(3);
    private static readonly DateTimeOffset WindowEnd = WindowStart.AddHours(4);
    private static readonly CancellationToken Ct = CancellationToken.None;
    private const string Policies =
        """{"Values":{"OCCUPANCY_VALIDITY_TIMESPAN":30,"ALLOCATION_LEAD_TIME":15}}""";

    [Fact]
    public async Task UpsertedBoundedSiteWide_PersistsClosureUnderLock_CancelsAndRefundsOverlapping_SkipsAdjacentAndParked()
    {
        await using var harness = new Harness();
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var slotOverlapping = Guid.NewGuid();
        var slotAdjacentBefore = Guid.NewGuid();
        var slotAdjacentAfter = Guid.NewGuid();
        var slotParked = Guid.NewGuid();

        var overlapping = ConfirmedBooking(tenantId, siteId, slotOverlapping, WindowStart.AddMinutes(30), WindowEnd.AddMinutes(-30));
        var endingAtStart = ConfirmedBooking(tenantId, siteId, slotAdjacentBefore, WindowStart.AddHours(-1), WindowStart);
        var startingAtEnd = ConfirmedBooking(tenantId, siteId, slotAdjacentAfter, WindowEnd, WindowEnd.AddHours(1));
        var parked = ConfirmedBooking(tenantId, siteId, slotParked, WindowStart.AddMinutes(15), WindowEnd);
        parked.EnterParking();
        var collectionId = Guid.NewGuid();
        harness.Commercial.Collections = [new VerifiedCollection(collectionId, overlapping.PaymentOrderId!.Value, overlapping.Id, overlapping.AccountId, tenantId, siteId, Guid.NewGuid(), overlapping.TermsVersion, 5_000m, "VND")];

        await harness.SeedAsync(
            overlapping,
            endingAtStart,
            startingAtEnd,
            parked,
            Allocation(overlapping, slotOverlapping, "RESERVED"),
            Allocation(parked, slotParked, "OCCUPIED"),
            Pool(tenantId, siteId, reserved: 4, pending: 0));

        var maintenanceId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        await harness.Handler().Handle(
            Event(eventId, maintenanceId, revision: 1, tenantId, siteId, "UPSERTED", "SITE",
                null, [], [], WindowStart, WindowEnd), Ct);

        // Site guard locked before writing projection.
        Assert.Contains("site", harness.Guards.Acquired);
        var closure = await harness.Db.StructureMaintenanceClosures.SingleAsync();
        Assert.Equal(maintenanceId, closure.WindowId);
        Assert.Equal(1L, closure.Revision);
        Assert.Equal(WindowEnd, closure.WindowEnd);
        Assert.Null(closure.CancelledAt);

        // Overlapping booking is cancelled + refunded; boundary-adjacent & PARKED stay intact.
        var cancelled = await harness.Db.Reservations.SingleAsync(r => r.Id == overlapping.Id);
        Assert.Equal("CANCELLED", cancelled.Status);
        Assert.Equal("FACILITY_FAILURE_RECONCILED", cancelled.ConfirmationCode);

        var keptBefore = await harness.Db.Reservations.SingleAsync(r => r.Id == endingAtStart.Id);
        var keptAfter = await harness.Db.Reservations.SingleAsync(r => r.Id == startingAtEnd.Id);
        var keptParked = await harness.Db.Reservations.SingleAsync(r => r.Id == parked.Id);
        Assert.Equal("CONFIRMED", keptBefore.Status);
        Assert.Equal("CONFIRMED", keptAfter.Status);
        Assert.Equal("PARKING", keptParked.Status);

        var call = Assert.Single(harness.Commercial.ReconcileCalls);
        Assert.Equal((overlapping.Id, collectionId, $"FACILITY_FAILURE:{maintenanceId:D}"), call);

        var receipt = await harness.Db.FacilityEventReceipts.SingleAsync();
        Assert.Equal(eventId, receipt.EventId);
        Assert.Equal("FacilityMaintenanceChangedV1", receipt.EventType);
    }

    [Fact]
    public async Task UpsertedOpenEndedNullEnd_PersistsNullWindowEnd_AndCancelsFarFutureBookingsOnly()
    {
        await using var harness = new Harness();
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var slotFuture = Guid.NewGuid();
        var slotPreStart = Guid.NewGuid();

        var farFuture = ConfirmedBooking(tenantId, siteId, slotFuture, WindowStart.AddDays(3), WindowStart.AddDays(3).AddHours(2));
        var preStartEndingAtStart = ConfirmedBooking(tenantId, siteId, slotPreStart, WindowStart.AddHours(-2), WindowStart);
        await harness.SeedAsync(farFuture, preStartEndingAtStart, Pool(tenantId, siteId, reserved: 2, pending: 0));

        var maintenanceId = Guid.NewGuid();
        await harness.Handler().Handle(
            Event(Guid.NewGuid(), maintenanceId, revision: 1, tenantId, siteId, "UPSERTED", "SITE",
                null, [], [], WindowStart, endAtUtc: null), Ct);

        var closure = await harness.Db.StructureMaintenanceClosures.SingleAsync();
        Assert.Null(closure.WindowEnd);
        Assert.Null(closure.CancelledAt);

        Assert.Equal("CANCELLED", (await harness.Db.Reservations.SingleAsync(r => r.Id == farFuture.Id)).Status);
        Assert.Equal("CONFIRMED", (await harness.Db.Reservations.SingleAsync(r => r.Id == preStartEndingAtStart.Id)).Status);
    }

    [Fact]
    public async Task ExtensionFromOpenEndedToBounded_UpdatesRevisionAndWindowEnd_AndReconcilesNewlyCoveredBookings()
    {
        await using var harness = new Harness();
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var maintenanceId = Guid.NewGuid();

        await harness.Handler().Handle(
            Event(Guid.NewGuid(), maintenanceId, revision: 1, tenantId, siteId, "UPSERTED", "SITE",
                null, [], [], WindowStart, WindowEnd), Ct);

        var extendedBooking = ConfirmedBooking(tenantId, siteId, Guid.NewGuid(), WindowEnd, WindowEnd.AddHours(2));
        await harness.SeedAsync(extendedBooking, Pool(tenantId, siteId, reserved: 1, pending: 0));

        await harness.Handler().Handle(
            Event(Guid.NewGuid(), maintenanceId, revision: 2, tenantId, siteId, "UPSERTED", "SITE",
                null, [], [], WindowStart, WindowEnd.AddHours(3)), Ct);

        var closure = await harness.Db.StructureMaintenanceClosures.SingleAsync();
        Assert.Equal(2L, closure.Revision);
        Assert.Equal(WindowEnd.AddHours(3), closure.WindowEnd);
        Assert.Equal("CANCELLED", (await harness.Db.Reservations.SingleAsync(r => r.Id == extendedBooking.Id)).Status);
    }

    [Fact]
    public async Task Cleared_MarksCancelled_AndBlocksStaleOlderUpsertedWithoutResurrection()
    {
        await using var harness = new Harness();
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var maintenanceId = Guid.NewGuid();

        await harness.Handler().Handle(
            Event(Guid.NewGuid(), maintenanceId, revision: 1, tenantId, siteId, "UPSERTED", "SITE",
                null, [], [], WindowStart, WindowEnd), Ct);

        await harness.Handler().Handle(
            Event(Guid.NewGuid(), maintenanceId, revision: 2, tenantId, siteId, "CLEARED", "SITE",
                null, [], [], WindowStart, WindowEnd), Ct);

        // Older rev 1 redelivered under a different transport EventId must be ignored.
        await harness.Handler().Handle(
            Event(Guid.NewGuid(), maintenanceId, revision: 1, tenantId, siteId, "UPSERTED", "SITE",
                null, [], [], WindowStart, WindowEnd.AddHours(2)), Ct);

        var closure = await harness.Db.StructureMaintenanceClosures.SingleAsync();
        Assert.Equal(2L, closure.Revision);
        Assert.NotNull(closure.CancelledAt);
        Assert.Equal(WindowEnd, closure.WindowEnd);
    }

    [Fact]
    public async Task OutOfOrderClearedArrivingFirst_WritesTombstone_AndRejectsOlderUpserted()
    {
        await using var harness = new Harness();
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var maintenanceId = Guid.NewGuid();
        var existingBooking = ConfirmedBooking(tenantId, siteId, Guid.NewGuid(), WindowStart, WindowEnd);
        await harness.SeedAsync(existingBooking, Pool(tenantId, siteId, reserved: 1, pending: 0));

        await harness.Handler().Handle(
            Event(Guid.NewGuid(), maintenanceId, revision: 2, tenantId, siteId, "CLEARED", "SITE",
                null, [], [], WindowStart, WindowEnd), Ct);

        // Delayed older revision 1 arrives after the tombstone.
        await harness.Handler().Handle(
            Event(Guid.NewGuid(), maintenanceId, revision: 1, tenantId, siteId, "UPSERTED", "SITE",
                null, [], [], WindowStart, WindowEnd), Ct);

        var closure = await harness.Db.StructureMaintenanceClosures.SingleAsync();
        Assert.Equal(2L, closure.Revision);
        Assert.NotNull(closure.CancelledAt);
        Assert.Equal("CONFIRMED", (await harness.Db.Reservations.SingleAsync(r => r.Id == existingBooking.Id)).Status);
    }

    [Fact]
    public async Task UnitAndSlotScopedMaintenance_ReallocatesToVerifiedFreeSlotWhenAvailable()
    {
        await using var harness = new Harness();
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var zoneA = Guid.NewGuid();
        var closedSlot = Guid.NewGuid();
        var backupSlot = Guid.NewGuid();

        var booking = ConfirmedBooking(tenantId, siteId, closedSlot, WindowStart.AddMinutes(30), WindowStart.AddHours(2));
        booking.TargetSpatialUnitId = zoneA;
        await harness.SeedAsync(booking, Allocation(booking, closedSlot, "RESERVED"), Pool(tenantId, siteId, reserved: 1, pending: 0));
        harness.Slots.Candidates =
        [
            new FacilitySlotCandidate(closedSlot, zoneA, "CAR", true, true, true, Backup: false),
            new FacilitySlotCandidate(backupSlot, Guid.NewGuid(), "CAR", true, true, true, Backup: true)
        ];

        await harness.Handler().Handle(
            Event(Guid.NewGuid(), Guid.NewGuid(), revision: 1, tenantId, siteId, "UPSERTED", "UNIT",
                zoneA, [zoneA], [closedSlot], WindowStart, WindowEnd), Ct);

        var stored = await harness.Db.Reservations.SingleAsync();
        Assert.Equal("CONFIRMED", stored.Status);
        Assert.Equal(backupSlot, stored.TargetSlotId);
        Assert.Equal(backupSlot, stored.ActiveClaimSlotId);
        Assert.Equal(backupSlot, (await harness.Db.SlotAllocations.SingleAsync()).SlotId);
        Assert.Empty(harness.Commercial.ReconcileCalls);
    }

    [Fact]
    public async Task SameEventIdRetryAfterTransientPaymentFailure_CompletesRefundWithoutDuplicateCancellation()
    {
        await using var harness = new Harness();
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var maintenanceId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var booking = ConfirmedBooking(tenantId, siteId, slotId, WindowStart, WindowEnd);
        var collectionId = Guid.NewGuid();
        harness.Commercial.Collections = [new VerifiedCollection(collectionId, booking.PaymentOrderId!.Value, booking.Id, booking.AccountId, tenantId, siteId, Guid.NewGuid(), booking.TermsVersion, 5_000m, "VND")];
        await harness.SeedAsync(booking, Allocation(booking, slotId, "RESERVED"), Pool(tenantId, siteId, reserved: 1, pending: 0));

        // Attempt 1 fails inside PaymentService; receipt must NOT be recorded.
        harness.Commercial.ReconcileFailure = _ => new HttpRequestException("PaymentService timeout");
        await Assert.ThrowsAsync<HttpRequestException>(() => harness.Handler().Handle(
            Event(eventId, maintenanceId, revision: 1, tenantId, siteId, "UPSERTED", "SITE",
                null, [], [], WindowStart, WindowEnd), Ct));

        Assert.Empty(await harness.Db.FacilityEventReceipts.ToListAsync());
        var afterFirst = await harness.Db.Reservations.SingleAsync();
        Assert.Equal("CANCELLED", afterFirst.Status);
        Assert.Equal("RECONCILIATION_REQUIRED", afterFirst.ConfirmationState);
        Assert.Equal(0, (await harness.Db.SiteCapacityPools.SingleAsync()).CurrentReservedCount);

        // Attempt 2 with same EventId succeeds and finishes Payment reconciliation.
        harness.Commercial.ReconcileFailure = null;
        await harness.Handler().Handle(
            Event(eventId, maintenanceId, revision: 1, tenantId, siteId, "UPSERTED", "SITE",
                null, [], [], WindowStart, WindowEnd), Ct);

        var call = Assert.Single(harness.Commercial.ReconcileCalls);
        Assert.Equal((booking.Id, collectionId, $"FACILITY_FAILURE:{maintenanceId:D}"), call);
        Assert.Equal("FACILITY_FAILURE_RECONCILED", (await harness.Db.Reservations.SingleAsync()).ConfirmationCode);
        Assert.Single(await harness.Db.FacilityEventReceipts.ToListAsync());

        // Attempt 3 duplicate EventId is completely ignored.
        await harness.Handler().Handle(
            Event(eventId, maintenanceId, revision: 1, tenantId, siteId, "UPSERTED", "SITE",
                null, [], [], WindowStart, WindowEnd), Ct);
        Assert.Single(harness.Commercial.ReconcileCalls);
    }

    [Fact]
    public async Task Consumer_DropsMalformedMessages_AndDispatchesValidEnvelope()
    {
        var mediator = new RecordingMediator();
        var consumer = new FacilityMaintenanceChangedConsumer(mediator);

        // Empty MaintenanceId -> dropped cleanly.
        await consumer.Consume(CreateConsumeContext(new FacilityMaintenanceChangedMessage(
            Guid.NewGuid(), 1, Guid.Empty, 1, Now, Guid.NewGuid(), Guid.NewGuid(),
            "UPSERTED", "SCHEDULED", "SITE", null, [], [], WindowStart, WindowEnd, "Bad")));
        Assert.Empty(mediator.Sent);

        // Wrong schema version -> dropped cleanly.
        await consumer.Consume(CreateConsumeContext(new FacilityMaintenanceChangedMessage(
            Guid.NewGuid(), 99, Guid.NewGuid(), 1, Now, Guid.NewGuid(), Guid.NewGuid(),
            "UPSERTED", "SCHEDULED", "SITE", null, [], [], WindowStart, WindowEnd, "Bad")));
        Assert.Empty(mediator.Sent);

        // End <= Start -> dropped cleanly.
        await consumer.Consume(CreateConsumeContext(new FacilityMaintenanceChangedMessage(
            Guid.NewGuid(), 1, Guid.NewGuid(), 1, Now, Guid.NewGuid(), Guid.NewGuid(),
            "UPSERTED", "SCHEDULED", "SITE", null, [], [], WindowStart, WindowStart, "Bad")));
        Assert.Empty(mediator.Sent);

        // Valid open-ended UPSERTED -> forwarded to mediator.
        var validId = Guid.NewGuid();
        await consumer.Consume(CreateConsumeContext(new FacilityMaintenanceChangedMessage(
            Guid.NewGuid(), 1, validId, 1, Now, Guid.NewGuid(), Guid.NewGuid(),
            "UPSERTED", "SCHEDULED", "SITE", null, [], [], WindowStart, null, "Valid")));
        var sent = Assert.IsType<ApplyFacilityMaintenanceChangedCommand>(Assert.Single(mediator.Sent));
        Assert.Equal(validId, sent.MaintenanceId);
        Assert.Null(sent.EndAtUtc);
    }

    private static ApplyFacilityMaintenanceChangedCommand Event(
        Guid eventId, Guid maintenanceId, long revision, Guid tenantId, Guid siteId,
        string changeType, string scope, Guid? unitId,
        IReadOnlyList<Guid> scopedUnitIds, IReadOnlyList<Guid> slotIds,
        DateTimeOffset startAtUtc, DateTimeOffset? endAtUtc) =>
        new(eventId, 1, maintenanceId, revision, Now, tenantId, siteId,
            changeType, "SCHEDULED", scope, unitId, scopedUnitIds, slotIds,
            startAtUtc, endAtUtc, "Scheduled resurfacing");

    private static ReservationRow ConfirmedBooking(
        Guid tenantId, Guid siteId, Guid slotId, DateTimeOffset start, DateTimeOffset end)
    {
        var booking = ReservationRow.Prepare(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            tenantId, siteId, slotId, Guid.NewGuid(), "terms-v1", "PAYOS",
            start, end, end, Now, Now.AddMinutes(30), 20_000m, 5_000m, "VND", Policies, "{}");
        booking.RecordOrderCreated(Guid.NewGuid());
        booking.ConfirmAtEvaluation(Guid.NewGuid(), Now.AddMinutes(1));
        return booking;
    }

    private static SlotAllocationRow Allocation(ReservationRow booking, Guid slotId, string status) => new()
    {
        Id = Guid.NewGuid(),
        ReservationId = booking.Id,
        TenantId = booking.TenantId,
        SiteId = booking.SiteId,
        SlotId = slotId,
        AllocationStatus = status,
        CreatedAt = Now,
        AllocatedStartTime = booking.ExpectedStartTime,
        AllocatedEndTime = booking.ExpectedEndTime
    };

    private static SiteCapacityPoolRow Pool(Guid tenantId, Guid siteId, int reserved, int pending) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        SiteId = siteId,
        SpatialUnitId = null,
        VehicleType = "CAR",
        TotalCapacity = 10,
        MaxReservationQuota = 10,
        CurrentReservedCount = reserved,
        PendingPaymentCount = pending,
        UpdatedAt = Now
    };

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

        public ApplyFacilityMaintenanceChangedCommandHandler Handler() =>
            new(Store, Bookings, Slots, Availability, Guards, Commercial, Work, Clock);

        public async Task SeedAsync(params object[] rows)
        {
            Db.AddRange(rows);
            await Db.SaveChangesAsync(Ct);
            Db.ChangeTracker.Clear();
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class RecordingMediator : IMediator
    {
        public List<object> Sent { get; } = [];
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
        {
            Sent.Add(request);
            return Task.CompletedTask;
        }
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private static ConsumeContext<FacilityMaintenanceChangedMessage> CreateConsumeContext(
        FacilityMaintenanceChangedMessage message)
    {
        var proxy = DispatchProxy.Create<ConsumeContext<FacilityMaintenanceChangedMessage>, MessageProxy>();
        ((MessageProxy)(object)proxy).Init(message);
        return proxy;
    }

    private class MessageProxy : DispatchProxy
    {
        private FacilityMaintenanceChangedMessage _message = null!;

        public void Init(FacilityMaintenanceChangedMessage message) => _message = message;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name switch
            {
                "get_Message" => _message,
                "get_CancellationToken" => Ct,
                "get_MessageId" => (Guid?)_message.EventId,
                "get_SentTime" => (DateTime?)_message.OccurredAtUtc.UtcDateTime,
                _ => targetMethod?.ReturnType == typeof(Task) ? Task.CompletedTask : null
            };
    }
}
