using SmartParking.OperationService.Domain.Reservations;
using SmartParking.FacilityService.Domain.Entities;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Tests;

public sealed class ReservationAvailabilityTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);
    private static CapacityDemand Demand(Guid slot) => new(Guid.NewGuid(), slot, Now.AddMinutes(10), Now.AddHours(1));

    [Theory]
    [InlineData(null)]
    [InlineData(10_000)]
    public void RejectsOnlyTheOccupiedNamedSlotAcrossTheWholeInterval(int? quota)
    {
        var first = Demand(Guid.NewGuid());
        var second = Demand(Guid.NewGuid());
        var middle = first.Start.AddMinutes(15);
        var existing = new[] { first with { End = middle }, second with { Start = middle } };
        AvailabilityDecision Check(Guid slot) => ReservationAvailability.Evaluate(Demand(slot),
            existing, [], quota, Now, TimeSpan.Zero);

        Assert.Equal("SLOT_TIME_CONFLICT", Check(first.Slot!.Value).Rejection);
        Assert.Equal("SLOT_TIME_CONFLICT", Check(second.Slot!.Value).Rejection);
        Assert.True(Check(Guid.NewGuid()).Allowed);
    }

    [Fact]
    public void NamedSlotUsesEachAcceptedBufferedEnd()
    {
        var first = Demand(Guid.NewGuid());
        var candidate = first with { Id = Guid.NewGuid(), Start = first.End, End = first.End.AddMinutes(30) };
        AvailabilityDecision Check(CapacityDemand existing) => ReservationAvailability.Evaluate(candidate,
            [existing], [], 1, Now, TimeSpan.Zero);

        Assert.True(Check(first).Allowed);
        Assert.Equal("SLOT_TIME_CONFLICT", Check(first with { End = first.End.AddMinutes(1) }).Rejection);
    }

    [Fact]
    public void LinkedVisitAndReservationConsumeOneCommercialUnit()
    {
        var occupied = Guid.NewGuid();
        var existing = Demand(occupied);
        var visit = new CapacityVisit(Guid.NewGuid(), existing.Id, occupied, Now);
        var allocation = new CapacityAllocation(occupied, existing.Start, existing.End,
            "OCCUPIED", existing.Id, visit.Id, Now);
        existing = existing with { Allocations = [allocation] };
        visit = visit with { Allocations = [allocation] };
        var candidate = Demand(Guid.NewGuid());
        Assert.True(ReservationAvailability.Evaluate(candidate, [existing], [visit],
            2, Now, TimeSpan.FromHours(4)).Allowed);
        Assert.Equal("RESERVATION_QUOTA_EXCEEDED", ReservationAvailability.Evaluate(candidate, [existing], [visit],
            1, Now, TimeSpan.FromHours(4)).Rejection);
    }

    [Fact]
    public void MissingQuotaAllowsAdmissionAndInvalidQuotaIsIndeterminate()
    {
        var candidate = Demand(Guid.NewGuid());
        Assert.True(ReservationAvailability.Evaluate(candidate, [], [], null, Now, TimeSpan.Zero).Allowed);
        var invalid = ReservationAvailability.Evaluate(candidate, [], [], -1, Now, TimeSpan.Zero);
        Assert.Equal("RESERVATION_QUOTA_CONFIGURATION_INVALID", invalid.Rejection);
        Assert.True(invalid.Indeterminate);
        var zero = ReservationAvailability.Evaluate(candidate, [], [], 0, Now, TimeSpan.Zero);
        Assert.Equal("RESERVATION_QUOTA_EXCEEDED", zero.Rejection);
        Assert.False(zero.Indeterminate);
    }

    [Fact]
    public void CommercialQuotaUsesOverlappingEffectiveIntervalsAndCountsTheCandidateOnce()
    {
        var candidate = Demand(Guid.NewGuid());
        var earlier = Demand(Guid.NewGuid()) with { Start = Now, End = candidate.Start };
        var later = Demand(Guid.NewGuid()) with { Start = candidate.Start.AddMinutes(5) };
        var otherCategory = Demand(Guid.NewGuid()) with { CountsQuota = false, Category = "MOTORCYCLE" };
        AvailabilityDecision Check(int quota, params CapacityDemand[] claims) =>
            ReservationAvailability.Evaluate(candidate, claims, [], quota, Now, TimeSpan.Zero);

        Assert.True(Check(1, candidate, earlier, otherCategory).Allowed);
        Assert.True(Check(2, earlier, later).Allowed);
        Assert.Equal("RESERVATION_QUOTA_EXCEEDED", Check(1, later).Rejection);
        Assert.Equal("RESERVATION_QUOTA_EXCEEDED", Check(1, earlier with { End = candidate.Start.AddMinutes(1) }).Rejection);
    }

    [Theory]
    [InlineData("CAR")]
    [InlineData("MOTORCYCLE")]
    public void UnlocatedWalkInNeverTakesTheLastSlotButObservedOccupancyDoes(string category)
    {
        var slot = Guid.NewGuid();
        var visit = new CapacityVisit(Guid.NewGuid(), null, null, Now, category);
        AvailabilityDecision Check(CapacityVisit occupant) => ReservationAvailability.Evaluate(Demand(slot), [],
            [occupant], null, Now, TimeSpan.FromHours(4));

        Assert.True(Check(visit).Allowed);
        Assert.Equal("CAPACITY_UNAVAILABLE", Check(visit with { Slot = slot }).Rejection);
    }

    [Fact]
    public void AllocationSupersedesOriginalTargetOnlyWhileAssigned()
    {
        var original = Guid.NewGuid();
        var assigned = Guid.NewGuid();
        var candidate = Demand(original);
        var changeAt = candidate.Start.AddMinutes(10);
        var existing = candidate with { Id = Guid.NewGuid(), TargetSlot = original };
        var visit = new CapacityVisit(Guid.NewGuid(), existing.Id, assigned, changeAt);
        var replacement = new CapacityAllocation(assigned, changeAt, candidate.End, "OCCUPIED",
            existing.Id, visit.Id, changeAt);
        existing = existing with { Allocations = [replacement] };
        AvailabilityDecision Check(CapacityDemand request, CapacityDemand[] claims, params CapacityVisit[] visits) =>
            ReservationAvailability.Evaluate(request, claims, visits, 4, Now, TimeSpan.FromHours(4));

        Assert.Equal("SLOT_TIME_CONFLICT", Check(candidate with { End = changeAt }, [existing], visit).Rejection);
        var later = candidate with { Start = changeAt };
        Assert.True(Check(later, [existing], visit).Allowed);
        Assert.Equal("SLOT_TIME_CONFLICT", Check(later with { Slot = assigned }, [existing], visit).Rejection);
        Assert.Equal("CAPACITY_UNAVAILABLE",
            Check(later, [existing], visit, new CapacityVisit(Guid.NewGuid(), null, original, Now)).Rejection);
        Assert.Equal("SLOT_TIME_CONFLICT", Check(later, [existing, Demand(original)], visit).Rejection);
        Assert.Equal("SLOT_TIME_CONFLICT", Check(later,
            [existing with { Allocations = [replacement with { Status = "RELEASED" }] }]).Rejection);
        Assert.Equal("SLOT_TIME_CONFLICT", Check(later with { Slot = assigned },
            [existing with { Slot = assigned }], visit).Rejection);
    }

    [Fact]
    public void ContradictoryEvidenceFencesOnlyTheInvolvedSlots()
    {
        var original = Guid.NewGuid();
        var assigned = Guid.NewGuid();
        var existing = Demand(original);
        var visit = new CapacityVisit(Guid.NewGuid(), existing.Id, assigned, Now);
        var replacement = new CapacityAllocation(assigned, existing.Start, existing.End, "OCCUPIED",
            existing.Id, visit.Id, Now);
        var contradictory = new[]
        {
            existing with { Allocations = [replacement with { Status = "RESERVED", ScannedAt = null }] },
            existing with { Allocations = [replacement, new(original, existing.Start, existing.End)] },
            existing with { Allocations = [replacement with { ReservationId = Guid.NewGuid() }] },
        };
        foreach (var claim in contradictory)
        {
            AvailabilityDecision Check(Guid slot) => ReservationAvailability.Evaluate(Demand(slot), [claim], [visit],
                null, Now, TimeSpan.FromHours(4));
            Assert.Equal("OCCUPANCY_RECONCILIATION_REQUIRED", Check(original).Rejection);
            Assert.Equal("OCCUPANCY_RECONCILIATION_REQUIRED", Check(assigned).Rejection);
            Assert.True(Check(Guid.NewGuid()).Allowed);
        }
    }

    [Fact]
    public void OrphanAllocationIsAnIndeterminateBlockForItsNamedSlot()
    {
        var slot = Guid.NewGuid();
        var candidate = Demand(slot);
        var orphan = new CapacityAllocation(slot, candidate.Start, candidate.End, ReservationId: Guid.NewGuid());
        AvailabilityDecision Evaluate(Guid target) => ReservationAvailability.Evaluate(candidate with { Slot = target },
            [], [], null, Now, TimeSpan.Zero, [orphan]);

        Assert.True(Evaluate(slot).Indeterminate);
        Assert.Equal("OCCUPANCY_RECONCILIATION_REQUIRED", Evaluate(slot).Rejection);
        Assert.True(Evaluate(Guid.NewGuid()).Allowed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WalkInOccupancyBlocksOnlyItsObservedSlotUntilProjectionEnds(bool legacy)
    {
        var slot = Guid.NewGuid();
        var horizon = Now.AddHours(4);
        var visit = new CapacityVisit(Guid.NewGuid(), null, slot, Now);
        if (legacy) visit = visit with { Allocations = [new(slot, Now, DateTimeOffset.MaxValue, "OCCUPIED", null, visit.Id, Now)] };
        AvailabilityDecision Check(Guid target, DateTimeOffset start) => ReservationAvailability.Evaluate(
            Demand(target) with { Start = start, End = start.AddMinutes(30) }, [], [visit], 1,
            Now, TimeSpan.FromHours(4));

        Assert.Equal("CAPACITY_UNAVAILABLE", Check(slot, horizon.AddMinutes(-1)).Rejection);
        Assert.True(Check(Guid.NewGuid(), Now).Allowed);
        Assert.True(Check(slot, horizon).Allowed);
    }

    [Fact]
    public void AllocationOutlastingItsBookingStillOwnsItsSlotWithoutUsingBookingQuota()
    {
        var allocated = Guid.NewGuid();
        var request = Demand(allocated);
        var prior = request with { Id = Guid.NewGuid(), End = request.Start, Start = Now,
            Allocations = [new(allocated, Now, request.End)] };
        AvailabilityDecision Check(Guid slot) => ReservationAvailability.Evaluate(request with { Slot = slot },
            [prior], [], 1, Now, TimeSpan.Zero);

        Assert.Equal("SLOT_TIME_CONFLICT", Check(allocated).Rejection);
        Assert.True(Check(Guid.NewGuid()).Allowed);
    }

}
