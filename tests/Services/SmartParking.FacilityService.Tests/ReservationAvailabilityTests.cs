using SmartParking.OperationService.Domain.Reservations;
using SmartParking.FacilityService.Domain.Entities;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Tests;

public sealed class ReservationAvailabilityTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);
    private static OperationalCapacityConfig Quota(int count) => new()
        { IsActive = true, VehicleType = "CAR", TotalPhysicalCapacity = count, MaxReservationQuota = count };
    private static CapacityResource Resource(Guid id, bool backup = false) => new(id, null, [], true, backup);
    private static CapacityDemand Demand(Guid slot, DateTimeOffset? start = null) =>
        new(Guid.NewGuid(), slot, null, start ?? Now.AddMinutes(10), Now.AddHours(1));

    [Fact]
    public void SameSelectedSlotOverlapLosesEvenWhenAnotherSlotIsFree()
    {
        var slot = Guid.NewGuid();
        var result = ReservationAvailability.Evaluate(Demand(slot), [Demand(slot)], [],
            [Resource(slot), Resource(Guid.NewGuid())], [], [Quota(2)], Now, TimeSpan.FromHours(4), hasScopedCapacityEvidence: true);
        Assert.Equal("SLOT_TIME_CONFLICT", result.Rejection);
    }

    [Fact]
    public void AdjacentIntervalsDoNotConflict()
    {
        var slot = Guid.NewGuid();
        var earlier = new CapacityDemand(Guid.NewGuid(), slot, null, Now, Now.AddMinutes(10));
        Assert.True(ReservationAvailability.Evaluate(Demand(slot), [earlier], [], [Resource(slot)], [],
            [Quota(1)], Now, TimeSpan.FromHours(4), hasScopedCapacityEvidence: true).Allowed);
    }

    [Fact]
    public void OccupiedBackupIsCountedOnceAndUnknownVisitStillConsumesCapacity()
    {
        var ids = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray();
        var resources = ids.Select((id, i) => Resource(id, i is 0 or 3)).ToArray();
        var visits = ids.Take(3).Select(id => new CapacityVisit(Guid.NewGuid(), null, id, Now)).ToArray();
        var claims = ids.Skip(4).Take(5).Select(id => Demand(id)).ToArray();
        Assert.True(ReservationAvailability.Evaluate(Demand(ids[9]), claims, visits, resources, [],
            [Quota(10)], Now, TimeSpan.FromHours(4), hasScopedCapacityEvidence: true).Allowed);
        Assert.False(ReservationAvailability.Evaluate(Demand(ids[9]), claims,
            visits.Append(new(Guid.NewGuid(), null, null, Now)).ToArray(), resources, [],
            [Quota(10)], Now, TimeSpan.FromHours(4), hasScopedCapacityEvidence: true).Allowed);
    }

    [Fact]
    public void LinkedVisitAndReservationConsumeOneResource()
    {
        var occupied = Guid.NewGuid(); var free = Guid.NewGuid();
        var existing = Demand(occupied);
        var visit = new CapacityVisit(Guid.NewGuid(), existing.Id, occupied, Now);
        Assert.True(ReservationAvailability.Evaluate(Demand(free), [existing], [visit],
            [Resource(occupied), Resource(free)], [], [Quota(2)], Now, TimeSpan.FromHours(4), hasScopedCapacityEvidence: true).Allowed);
    }

    [Fact]
    public void MissingQuotaFailsClosed()
    {
        var slot = Guid.NewGuid();
        Assert.Equal("CAPACITY_CONFIGURATION_REQUIRED", ReservationAvailability.Evaluate(Demand(slot),
            [], [], [Resource(slot)], [], [], Now, TimeSpan.FromHours(4), hasScopedCapacityEvidence: true).Rejection);
    }

    [Fact]
    public void UnknownMotorcycleCannotConsumeAnIncompatibleCarOnlySlot()
    {
        var mixed = Guid.NewGuid(); var carOnly = Guid.NewGuid();
        var resources = new CapacityResource[]
        {
            new(mixed, null, [], true, false, ["CAR", "MOTORCYCLE"]),
            new(carOnly, null, [], true, false, ["CAR"])
        };
        var visit = new CapacityVisit(Guid.NewGuid(), null, null, Now, "MOTORCYCLE");
        Assert.False(ReservationAvailability.Evaluate(Demand(mixed), [], [visit], resources, [],
            [Quota(2)], Now, TimeSpan.FromHours(4), hasScopedCapacityEvidence: true).Allowed);
        Assert.True(ReservationAvailability.Evaluate(Demand(carOnly), [], [visit], resources, [],
            [Quota(2)], Now, TimeSpan.FromHours(4), hasScopedCapacityEvidence: true).Allowed);
    }

    [Theory]
    [InlineData(2, false, false)]
    [InlineData(3, false, true)]
    [InlineData(3, true, false)]
    public void NamedBookingPreservesUnassignedZoneDemand(int count, bool lastIsBackup, bool allowed)
    {
        var zone = Guid.NewGuid();
        var resources = Enumerable.Range(0, count).Select(i =>
            new CapacityResource(Guid.NewGuid(), zone, [], true, lastIsBackup && i == count - 1)).ToArray();
        var proposed = Demand(resources[1].Id);
        var scoped = proposed with { Id = Guid.NewGuid(), Slot = null, Unit = zone };
        // A stale ancestor maximum cannot veto a feasible named claim.
        var narrower = Quota(1);
        narrower.SpatialUnitId = zone;
        var result = ReservationAvailability.Evaluate(proposed, [Demand(resources[0].Id), scoped], [],
            resources, [], [Quota(4), narrower], Now, TimeSpan.Zero, hasScopedCapacityEvidence: true);
        Assert.Equal(allowed, result.Allowed);
        Assert.False(result.Indeterminate);
    }

    [Fact]
    public void AlreadyAllocatedBackupConsumesOnlyThatBackup()
    {
        var backup = Resource(Guid.NewGuid(), true);
        var ordinary = Resource(Guid.NewGuid());
        var claim = Demand(backup.Id);
        var allocated = claim with { Slot = null, Unit = Guid.NewGuid(), Allocations = [new(backup.Id, claim.Start, claim.End)] };
        Assert.True(ReservationAvailability.Evaluate(Demand(ordinary.Id), [allocated], [], [ordinary, backup],
            [], [Quota(2)], Now, TimeSpan.Zero, hasScopedCapacityEvidence: false).Allowed);
        Assert.Equal("SLOT_UNAVAILABLE", ReservationAvailability.Evaluate(Demand(backup.Id), [], [], [ordinary, backup],
            [], [Quota(2)], Now, TimeSpan.Zero, hasScopedCapacityEvidence: true).Rejection);
    }

    [Fact]
    public void FloorSpareCanServeFloorButCannotServeAnotherZone()
    {
        var floor = Guid.NewGuid();
        var zone = Guid.NewGuid();
        var a = new CapacityResource(Guid.NewGuid(), zone, [floor], true, false);
        var b = a with { Id = Guid.NewGuid(), Unit = Guid.NewGuid() };
        var c = b with { Id = Guid.NewGuid() };
        var proposed = Demand(a.Id);
        var unassigned = proposed with { Id = Guid.NewGuid(), Slot = null, Unit = floor };
        var narrow = unassigned with { Id = Guid.NewGuid(), Unit = zone };
        AvailabilityDecision Evaluate(CapacityDemand other) => ReservationAvailability.Evaluate(proposed,
            [unassigned, other], [], [a, b, c], [], [Quota(3)], Now, TimeSpan.Zero, true);
        Assert.False(Evaluate(narrow).Allowed);
        Assert.True(Evaluate(narrow with { Unit = b.Unit }).Allowed);
    }

    [Fact]
    public void MissingScopedEvidenceOnlyBlocksAnOverlappingUnallocatedUnit()
    {
        var a = Resource(Guid.NewGuid());
        var b = Resource(Guid.NewGuid());
        var proposed = Demand(a.Id);
        var scoped = proposed with { Id = Guid.NewGuid(), Slot = null, Unit = Guid.NewGuid() };
        AvailabilityDecision Evaluate(CapacityDemand other) => ReservationAvailability.Evaluate(proposed,
            [other], [], [a, b], [], [Quota(2)], Now, TimeSpan.Zero, hasScopedCapacityEvidence: false);
        Assert.True(Evaluate(scoped).Indeterminate);
        var linked = new CapacityVisit(Guid.NewGuid(), scoped.Id, b.Id, Now);
        Assert.Equal("SCOPED_CAPACITY_EVIDENCE_REQUIRED", ReservationAvailability.Evaluate(
            proposed, [scoped], [linked], [a, b], [], [Quota(2)], Now,
            TimeSpan.FromHours(4), hasScopedCapacityEvidence: false).Rejection);
        Assert.True(Evaluate(scoped with { Start = proposed.End, End = proposed.End.AddHours(1) }).Allowed);
        Assert.True(Evaluate(Demand(b.Id)).Allowed);
    }

    [Fact]
    public void OrdinaryFutureAssignmentConstrainsTheWholeScopedBooking()
    {
        var zone = Guid.NewGuid();
        var a = Resource(Guid.NewGuid()) with { Unit = zone };
        var b = a with { Id = Guid.NewGuid() };
        var named = Demand(a.Id);
        var middle = named.Start.AddMinutes(10);
        var scoped = named with { Id = Guid.NewGuid(), Slot = null, Unit = zone,
            Allocations = [new(a.Id, middle, named.End)] };
        AvailabilityDecision Evaluate(CapacityDemand candidate) => ReservationAvailability.Evaluate(candidate,
            [scoped], [], [a, b], [], [Quota(2)], Now, TimeSpan.Zero, true);
        Assert.False(Evaluate(named with { End = middle }).Allowed);
        Assert.False(Evaluate(named).Allowed);
        Assert.True(Evaluate(named with { Slot = b.Id }).Allowed);
        var visit = new CapacityVisit(Guid.NewGuid(), scoped.Id, a.Id, middle);
        var observed = new CapacityAllocation(a.Id, middle, named.End, "OCCUPIED", scoped.Id, visit.Id, middle);
        Assert.False(ReservationAvailability.Evaluate(named with { End = middle },
            [scoped with { Allocations = [observed] }], [visit], [a, b], [], [Quota(2)], Now,
            TimeSpan.FromHours(4), true).Allowed);
    }

    [Fact]
    public void AllocationSupersedesOriginalTargetOnlyWhileAssigned()
    {
        var original = Resource(Guid.NewGuid());
        var assigned = Resource(Guid.NewGuid());
        var candidate = Demand(original.Id);
        var changeAt = candidate.Start.AddMinutes(10);
        var existing = candidate with { Id = Guid.NewGuid(), TargetSlot = original.Id };
        var visit = new CapacityVisit(Guid.NewGuid(), existing.Id, assigned.Id, changeAt);
        var replacement = new CapacityAllocation(assigned.Id, changeAt, candidate.End, "OCCUPIED",
            existing.Id, visit.Id, changeAt);
        existing = existing with { Allocations = [replacement] };
        AvailabilityDecision Check(CapacityDemand request, params CapacityVisit[] others) => ReservationAvailability.Evaluate(
            request, [existing], [visit, .. others], [original, assigned], [], [Quota(2)], Now, TimeSpan.FromHours(4), false);

        Assert.Equal("SLOT_TIME_CONFLICT", Check(candidate with { End = changeAt }).Rejection);
        Assert.True(Check(candidate with { Start = changeAt }).Allowed);
        Assert.Equal("SLOT_TIME_CONFLICT", Check(candidate with { Slot = assigned.Id, Start = changeAt }).Rejection);
        var later = candidate with { Start = changeAt };
        Assert.False(Check(later, new CapacityVisit(Guid.NewGuid(), null, original.Id, Now)).Allowed);
        Assert.Equal("SLOT_TIME_CONFLICT", ReservationAvailability.Evaluate(later, [existing, Demand(original.Id)],
            [visit], [original, assigned], [], [Quota(4)], Now, TimeSpan.FromHours(4), false).Rejection);
        Assert.Equal("OCCUPANCY_RECONCILIATION_REQUIRED", ReservationAvailability.Evaluate(later,
            [existing with { Allocations = [replacement with { Status = "RESERVED", ScannedAt = null }] }],
            [], [original, assigned], [], [Quota(2)], Now, TimeSpan.Zero, false).Rejection);
        Assert.Equal("SLOT_TIME_CONFLICT", ReservationAvailability.Evaluate(later,
            [existing with { Allocations = [replacement with { Status = "RELEASED" }] }],
            [], [original, assigned], [], [Quota(2)], Now, TimeSpan.Zero, false).Rejection);
        Assert.Equal("OCCUPANCY_RECONCILIATION_REQUIRED", ReservationAvailability.Evaluate(later,
            [existing with { Allocations = [replacement, new(original.Id, changeAt, candidate.End)] }],
            [visit], [original, assigned], [], [Quota(2)], Now, TimeSpan.FromHours(4), false).Rejection);
        foreach (var excluded in new[] { original with { Backup = true },
                     original with { Exclusions = [new(changeAt, candidate.End)] } })
            Assert.Equal("SLOT_UNAVAILABLE", ReservationAvailability.Evaluate(later, [existing], [visit],
                [excluded, assigned], [], [Quota(2)], Now, TimeSpan.FromHours(4), true).Rejection);
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public void StaggeredBlockersNeedOneContinuousSlot(int count, bool feasible)
    {
        var zone = Guid.NewGuid();
        var slots = Enumerable.Range(0, count).Select(_ => Resource(Guid.NewGuid()) with { Unit = zone }).ToArray();
        var middle = Now.AddHours(1);
        var end = middle.AddHours(1);
        var flexible = new CapacityDemand(Guid.NewGuid(), null, zone, Now, end);
        var first = Demand(slots[0].Id) with { Start = Now, End = middle };
        var second = Demand(slots[1].Id) with { Start = middle, End = end };
        AvailabilityDecision Check(CapacityDemand request, CapacityDemand[] existing, CapacityResource[] supply) =>
            ReservationAvailability.Evaluate(request, existing, [], supply, [], [Quota(4)], Now, TimeSpan.Zero, true);
        Assert.Equal(feasible, Check(flexible, [first, second], slots).Allowed);
        Assert.Equal(feasible, Check(second, [first, flexible], slots).Allowed);
        if (feasible)
        {
            var restricted = slots.ToArray();
            restricted[2] = restricted[2] with { Exclusions = [new(Now.AddMinutes(30), middle)] };
            Assert.False(Check(second, [first, flexible], restricted).Allowed);
            Assert.True(ReservationAvailability.Evaluate(second, [first, flexible], [], slots, [new(zone, 1)],
                [Quota(4)], Now, TimeSpan.Zero, true).Allowed);
            Assert.False(ReservationAvailability.Evaluate(second, [first, flexible], [], slots, [new(zone, 2)],
                [Quota(4)], Now, TimeSpan.Zero, true).Allowed);
        }
    }

    [Fact]
    public void OverlapChainOutsideRequestCanDetermineItsOnlyCompatibleSlot()
    {
        // The request occupies A in hour 1. R1 is forced onto B in hours 1-2;
        // that forces R2 onto C in hours 2-3, where a non-overlapping named blocker already owns C.
        var a = Resource(Guid.NewGuid()) with { CompatibleCategories = ["CAR"] };
        var b = Resource(Guid.NewGuid()) with { CompatibleCategories = ["CAR", "VAN"] };
        var c = Resource(Guid.NewGuid()) with { CompatibleCategories = ["VAN"] };
        var request = Demand(a.Id) with { Start = Now, End = Now.AddHours(1) };
        var r1 = request with { Id = Guid.NewGuid(), Slot = null, End = Now.AddHours(2) };
        var r2 = r1 with { Id = Guid.NewGuid(), Category = "VAN", Start = request.End, End = Now.AddHours(3) };
        var blocker = r2 with { Id = Guid.NewGuid(), Slot = c.Id, Start = r1.End };
        AvailabilityDecision Check(params CapacityDemand[] existing) => ReservationAvailability.Evaluate(request,
            existing, [], [a, b, c], [], [Quota(4)], Now, TimeSpan.Zero, true);
        Assert.False(Check(r1, r2, blocker).Allowed);
        Assert.True(Check(r1, r2).Allowed);
        Assert.True(Check(r1, r2, blocker with { Start = r2.End, End = r2.End.AddHours(1) }).Allowed);
    }

    [Fact]
    public void JointAssignmentCanBacktrackAndIgnoreDisconnectedInfeasibility()
    {
        var zone = Guid.NewGuid();
        var remote = Guid.NewGuid();
        var a = Resource(Guid.NewGuid()) with { Unit = zone, CompatibleCategories = ["CAR", "VAN"] };
        var b = a with { Id = Guid.NewGuid(), CompatibleCategories = ["CAR"] };
        var c = a with { Id = Guid.NewGuid(), CompatibleCategories = ["VAN"] };
        var d = a with { Id = Guid.NewGuid(), Unit = remote };
        var car = Demand(a.Id) with { Slot = null, Unit = zone };
        var van = car with { Id = Guid.NewGuid(), Category = "VAN" };
        var independent = car with { Id = Guid.NewGuid(), Slot = d.Id, Unit = remote };
        Assert.True(ReservationAvailability.Evaluate(car, [van, van with { Id = Guid.NewGuid() },
            independent, independent with { Id = Guid.NewGuid() }], [], [a, b, c, d], [], [Quota(8)],
            Now, TimeSpan.Zero, true).Allowed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WalkInUsesOneProjectedUnitWithOrWithoutLegacyScanRow(bool legacy)
    {
        var a = Resource(Guid.NewGuid());
        var b = Resource(Guid.NewGuid());
        var horizon = Now.AddHours(4);
        var visit = new CapacityVisit(Guid.NewGuid(), null, a.Id, Now);
        if (legacy) visit = visit with { Allocations = [new(a.Id, Now, DateTimeOffset.MaxValue, "OCCUPIED", null, visit.Id, Now)] };
        AvailabilityDecision Check(Guid slot, DateTimeOffset start, CapacityVisit occupant, params CapacityResource[] supply) =>
            ReservationAvailability.Evaluate(Demand(slot) with { Start = start, End = start.AddMinutes(30) },
                [], [occupant], supply, [], [Quota(2)], Now, TimeSpan.FromHours(4), false);
        Assert.False(Check(a.Id, horizon.AddMinutes(-1), visit, a, b).Allowed);
        Assert.True(Check(b.Id, Now, visit, a, b).Allowed);
        Assert.True(Check(a.Id, horizon, visit, a).Allowed);
        var unlocated = visit with { Slot = null, Allocations = null };
        Assert.False(Check(a.Id, Now, unlocated, a).Allowed);
        Assert.True(Check(a.Id, Now, unlocated, a, b).Allowed);
    }

    [Fact]
    public void AllocationOutlastingItsBookingStillOwnsItsSlotWithoutUsingBookingQuota()
    {
        var allocated = Resource(Guid.NewGuid());
        var spare = Resource(Guid.NewGuid());
        var request = Demand(allocated.Id);
        var prior = request with { Id = Guid.NewGuid(), End = request.Start, Start = Now,
            Allocations = [new(allocated.Id, Now, request.End)] };
        AvailabilityDecision Check(Guid slot) => ReservationAvailability.Evaluate(request with { Slot = slot },
            [prior], [], [allocated, spare], [], [Quota(1)], Now, TimeSpan.Zero, hasScopedCapacityEvidence: false);

        Assert.Equal("SLOT_TIME_CONFLICT", Check(allocated.Id).Rejection);
        Assert.True(Check(spare.Id).Allowed);
    }

    [Fact]
    public void NamedSlotUsesEachAcceptedBufferedEnd()
    {
        var slot = Resource(Guid.NewGuid());
        var first = Demand(slot.Id);
        var candidate = first with { Id = Guid.NewGuid(), Start = first.End, End = first.End.AddMinutes(30) };
        AvailabilityDecision Evaluate(CapacityDemand existing) => ReservationAvailability.Evaluate(candidate,
            [existing], [], [slot], [], [Quota(1)], Now, TimeSpan.Zero, false);
        Assert.True(Evaluate(first).Allowed);
        Assert.Equal("SLOT_TIME_CONFLICT", Evaluate(first with { End = first.End.AddMinutes(1) }).Rejection);
    }

    [Fact]
    public void ProducerTombstoneRejectsDelayedAcquireAndCompletedOutcomeRejectsAbort()
    {
        ReservationDependency New() => ReservationDependency.Create(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "ADMISSION", "fingerprint", Now.AddMinutes(1), Now);
        var aborted = New(); aborted.Abort(Now);
        Assert.Throws<StructureException>(() => aborted.Hold("{}", Now));
        var committed = New(); committed.Hold("{}", Now);
        committed.Complete("same", Now.AddHours(1));
        committed.Complete("same", Now.AddHours(2));
        Assert.Throws<StructureException>(() => committed.Abort(Now.AddHours(3)));
        Assert.Throws<StructureException>(() => committed.Complete("different", Now.AddHours(3)));
    }
}
