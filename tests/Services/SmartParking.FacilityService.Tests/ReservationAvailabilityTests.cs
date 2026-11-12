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
            [Resource(slot), Resource(Guid.NewGuid())], [], [Quota(2)], Now, TimeSpan.FromHours(4), false);
        Assert.Equal("SLOT_TIME_CONFLICT", result.Rejection);
    }

    [Fact]
    public void AdjacentIntervalsDoNotConflict()
    {
        var slot = Guid.NewGuid();
        var earlier = new CapacityDemand(Guid.NewGuid(), slot, null, Now, Now.AddMinutes(10));
        Assert.True(ReservationAvailability.Evaluate(Demand(slot), [earlier], [], [Resource(slot)], [],
            [Quota(1)], Now, TimeSpan.FromHours(4), false).Allowed);
    }

    [Fact]
    public void OccupiedBackupIsCountedOnceAndUnknownVisitStillConsumesCapacity()
    {
        var ids = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray();
        var resources = ids.Select((id, i) => Resource(id, i is 0 or 3)).ToArray();
        var visits = ids.Take(3).Select(id => new CapacityVisit(Guid.NewGuid(), null, id, Now)).ToArray();
        var claims = ids.Skip(4).Take(5).Select(id => Demand(id)).ToArray();
        Assert.True(ReservationAvailability.Evaluate(Demand(ids[9]), claims, visits, resources, [],
            [Quota(10)], Now, TimeSpan.FromHours(4), false).Allowed);
        Assert.False(ReservationAvailability.Evaluate(Demand(ids[9]), claims,
            visits.Append(new(Guid.NewGuid(), null, null, Now)).ToArray(), resources, [],
            [Quota(10)], Now, TimeSpan.FromHours(4), false).Allowed);
    }

    [Fact]
    public void LinkedVisitAndReservationConsumeOneResource()
    {
        var occupied = Guid.NewGuid(); var free = Guid.NewGuid();
        var existing = Demand(occupied);
        var visit = new CapacityVisit(Guid.NewGuid(), existing.Id, occupied, Now);
        Assert.True(ReservationAvailability.Evaluate(Demand(free), [existing], [visit],
            [Resource(occupied), Resource(free)], [], [Quota(2)], Now, TimeSpan.FromHours(4), false).Allowed);
    }

    [Fact]
    public void MissingQuotaFailsClosed()
    {
        var slot = Guid.NewGuid();
        Assert.Equal("CAPACITY_CONFIGURATION_REQUIRED", ReservationAvailability.Evaluate(Demand(slot),
            [], [], [Resource(slot)], [], [], Now, TimeSpan.FromHours(4), false).Rejection);
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
            [Quota(2)], Now, TimeSpan.FromHours(4), false).Allowed);
        Assert.True(ReservationAvailability.Evaluate(Demand(carOnly), [], [visit], resources, [],
            [Quota(2)], Now, TimeSpan.FromHours(4), false).Allowed);
    }

    [Fact]
    public void NarrowerQuotaConstrainsItsDescendantSlots()
    {
        var parent = Guid.NewGuid(); var unit = Guid.NewGuid();
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        var narrower = Quota(1); narrower.SpatialUnitId = parent;
        var resources = new CapacityResource[]
        { new(first, unit, [parent], true, false), new(second, unit, [parent], true, false) };
        Assert.Equal("RESERVATION_QUOTA_EXCEEDED", ReservationAvailability.Evaluate(Demand(first),
            [Demand(second)], [], resources, [], [Quota(2), narrower], Now, TimeSpan.FromHours(4), false).Rejection);
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
