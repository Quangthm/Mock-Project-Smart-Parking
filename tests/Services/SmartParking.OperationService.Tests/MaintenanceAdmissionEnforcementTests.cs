using System.Text.Json;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Tests;

public sealed class MaintenanceAdmissionEnforcementTests
{
    private static readonly DateTimeOffset EvaluatedAt = new(2026, 11, 20, 10, 0, 0, TimeSpan.Zero);
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task OverlappingSiteWideClosure_BlocksAdmissionWithSlotUnavailable()
    {
        var setup = Setup();

        var error = await Assert.ThrowsAsync<ReservationGateException>(() =>
            setup.Handler(Closure(setup)).Handle(setup.Command, Ct));

        Assert.Equal(409, error.StatusCode);
        Assert.Equal("SLOT_UNAVAILABLE", error.Code);
        Assert.Equal("The selected parking area is closed for scheduled maintenance during the requested period.",
            error.Message);
    }

    [Fact]
    public async Task ClosureTouchingProposedEnd_AllowsAdmission()
    {
        var setup = Setup();
        var touching = Closure(setup, windowStart: setup.ProposedEnd, windowEnd: setup.ProposedEnd.AddHours(2));

        await setup.Handler(touching).Handle(setup.Command, Ct);
    }

    [Fact]
    public async Task SlotScopedClosure_BlocksTheMatchingSlot()
    {
        var setup = Setup();

        var error = await Assert.ThrowsAsync<ReservationGateException>(() =>
            setup.Handler(Closure(setup, slotId: setup.SlotId)).Handle(setup.Command, Ct));

        Assert.Equal(409, error.StatusCode);
        Assert.Equal("SLOT_UNAVAILABLE", error.Code);
    }

    [Fact]
    public async Task SlotScopedClosure_AllowsADifferentSlot()
    {
        var setup = Setup();

        await setup.Handler(Closure(setup, slotId: Guid.NewGuid())).Handle(setup.Command, Ct);
    }

    [Fact]
    public async Task SlotScopedClosureInSameZone_DoesNotBlockAnotherSlot()
    {
        var zoneA = Guid.NewGuid();
        var setup = Setup(proposedUnitId: zoneA, resourceUnitId: zoneA);

        await setup.Handler(Closure(setup, unitId: zoneA, slotId: Guid.NewGuid()))
            .Handle(setup.Command, Ct);
    }

    [Fact]
    public async Task UnitScopedClosureViaScopedSlotIds_BlocksTheDescendantSlot()
    {
        var setup = Setup();

        var error = await Assert.ThrowsAsync<ReservationGateException>(() => setup
            .Handler(Closure(setup, unitId: Guid.NewGuid(), scopedSlotIds: [setup.SlotId]))
            .Handle(setup.Command, Ct));

        Assert.Equal(409, error.StatusCode);
        Assert.Equal("SLOT_UNAVAILABLE", error.Code);
    }

    [Fact]
    public async Task UnitScopedClosureViaAncestorUnit_BlocksTheDescendantSlot()
    {
        var unitId = Guid.NewGuid();
        var setup = Setup(ancestorUnitIds: [unitId]);

        var error = await Assert.ThrowsAsync<ReservationGateException>(() => setup
            .Handler(Closure(setup, unitId: unitId))
            .Handle(setup.Command, Ct));

        Assert.Equal(409, error.StatusCode);
        Assert.Equal("SLOT_UNAVAILABLE", error.Code);
    }

    [Fact]
    public async Task CancelledClosure_DoesNotBlockAdmission()
    {
        var setup = Setup();

        await setup.Handler(Closure(setup, cancelledAt: EvaluatedAt)).Handle(setup.Command, Ct);
    }

    private static StructureMaintenanceClosureRow Closure(AdmissionSetup setup, Guid? unitId = null, Guid? slotId = null,
        DateTimeOffset? windowStart = null, DateTimeOffset? windowEnd = null,
        IReadOnlyList<Guid>? scopedUnitIds = null, IReadOnlyList<Guid>? scopedSlotIds = null,
        DateTimeOffset? cancelledAt = null) =>
        new()
        {
            WindowId = Guid.NewGuid(), TenantId = setup.TenantId, SiteId = setup.SiteId,
            UnitId = unitId, SlotId = slotId,
            ScopedUnitIds = scopedUnitIds is null ? null : JsonSerializer.Serialize(scopedUnitIds),
            ScopedSlotIds = scopedSlotIds is null ? null : JsonSerializer.Serialize(scopedSlotIds),
            WindowStart = windowStart ?? setup.Start, WindowEnd = windowEnd ?? setup.End,
            Reason = "scheduled maintenance", EnforcementRef = Guid.NewGuid(), AcknowledgedAt = EvaluatedAt,
            CancelledAt = cancelledAt
        };

    private static AdmissionSetup Setup(IReadOnlyList<Guid>? ancestorUnitIds = null,
        Guid? proposedUnitId = null, Guid? resourceUnitId = null)
    {
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var start = EvaluatedAt.AddHours(1);
        var end = start.AddHours(1);
        const string policies = """{"Values":{"OCCUPANCY_VALIDITY_TIMESPAN":30,"ALLOCATION_LEAD_TIME":15}}""";
        var proposed = ReservationRow.Prepare(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            tenantId, siteId, slotId, Guid.NewGuid(), "terms-v1", "PAYOS", start, end, end, EvaluatedAt,
            start, 20_000m, 5_000m, "VND", policies, "{}");
        proposed.TargetSpatialUnitId = proposedUnitId;
        var snapshot = new FacilityResourceSnapshot(
            new FacilityBookingFacts(tenantId, siteId, slotId, "v1", EligibleCarSlot: true,
                ParticipatesInStructureGuard: true),
            [new FacilityResourceFact(slotId, resourceUnitId, "CAR", Active: true, Reachable: true, FixedBackup: false,
                AncestorUnitIds: ancestorUnitIds)],
            [], Complete: true);
        var quotas = new OperationalCapacityConfig[]
        {
            new()
            {
                Id = Guid.NewGuid(), TenantId = tenantId, SiteId = siteId, SpatialUnitId = null,
                VehicleType = "CAR", TotalPhysicalCapacity = 1, MaxReservationQuota = 1, IsActive = true,
                CreatedAt = EvaluatedAt, UpdatedAt = EvaluatedAt
            }
        };
        return new AdmissionSetup(tenantId, siteId, slotId, proposed, snapshot, quotas, start, end);
    }

    private sealed record AdmissionSetup(Guid TenantId, Guid SiteId, Guid SlotId, ReservationRow Proposed,
        FacilityResourceSnapshot Snapshot, IReadOnlyList<OperationalCapacityConfig> Quotas,
        DateTimeOffset Start, DateTimeOffset End)
    {
        public DateTimeOffset ProposedEnd => Proposed.ClaimEndTime ?? Proposed.ExpectedEndTime;

        public RequireReservationAvailabilityCommand Command => new(Proposed, Snapshot, EvaluatedAt);

        public RequireReservationAvailabilityHandler Handler(params StructureMaintenanceClosureRow[] closures) =>
            new(new FakeAvailabilityRepository(
                new ReservationAvailabilityFacts([], [], [], Quotas, closures)));
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
}
