using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Tests;

public sealed class DiscoveryAvailabilityTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 8, 0, 0, TimeSpan.Zero);
    private static readonly ReservationInterval Stay = new(Now.AddHours(1), Now.AddHours(2));

    [Theory]
    [InlineData(null, 3)]
    [InlineData(0, 0)]
    [InlineData(3, 1)]
    [InlineData(-1, null)]
    public void PeakQuotaIncludesBufferedTailsAndOnlyMatchingCategory(int? quota, int? expected)
    {
        Guid[] free = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        var buffered = new ReservationInterval(Stay.Start, Stay.End.AddMinutes(5));
        var middle = Stay.End.AddMinutes(2);
        ReservationRow Claim(DateTimeOffset start, DateTimeOffset end, string category = "CAR") => new()
        {
            Id = Guid.NewGuid(), TargetSlotId = Guid.NewGuid(), VehicleType = category,
            ExpectedStartTime = start, ExpectedEndTime = end
        };
        var first = Claim(Stay.Start, middle.AddMinutes(1));
        var facts = new ReservationAvailabilityFacts([first, Claim(middle, buffered.End),
            Claim(Stay.Start, buffered.End, "MOTORCYCLE"), Claim(buffered.End, buffered.End.AddHours(1))], [], [], quota);
        Assert.Equal(expected, facts.CountAvailableCars(free, buffered, Now, TimeSpan.Zero, default));
    }

    [Fact]
    public void AvailabilityRequiresTheSameSlotForEverySegment()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var middle = Stay.Start.AddMinutes(30);
        CapacityDemand[] bookings = [new(Guid.NewGuid(), first, Stay.Start, middle),
            new(Guid.NewGuid(), second, middle, Stay.End)];
        Assert.Equal(0, ReservationAvailability.CountAvailable([first, second], Stay, Now,
            TimeSpan.Zero, bookings, [], [], null, default));
    }

    [Fact]
    public void CurrentIgnoresQuotaAndHorizonAndDiscoveryExcludesNoArrivingSession()
    {
        var located = Guid.NewGuid();
        var free = Guid.NewGuid();
        var visit = new ParkingSessionRow { Id = Guid.NewGuid(), EntryTime = Now.AddDays(-1),
            VehicleType = "CAR", CurrentSlotId = located, LocationScannedAt = Now.AddMinutes(-1) };
        var unlocated = new ParkingSessionRow { Id = Guid.NewGuid(), EntryTime = Now, VehicleType = "CAR" };
        var facts = new ReservationAvailabilityFacts([], [visit, unlocated], [], 0);
        Assert.Equal(1, facts.CountAvailableCars([located, free], null, Now, TimeSpan.Zero, default));
        Assert.Equal(0, facts.CountAvailableCars([located, free], Stay, Now, TimeSpan.Zero, default));
        Assert.Equal(2, (facts with { MaxReservationQuota = null }).CountAvailableCars(
            [located, free], Stay, Now, TimeSpan.FromMinutes(20), default));
        Assert.Null(visit.ExitTime);
    }

    [Fact]
    public void UnresolvedAllocationMakesExactCountUnknownOnlyForAffectedCandidates()
    {
        var slot = Guid.NewGuid();
        var other = Guid.NewGuid();
        var facts = new ReservationAvailabilityFacts([], [], [new SlotAllocationRow {
            Id = Guid.NewGuid(), SlotId = slot, AllocationStatus = "RESERVED",
            AllocatedStartTime = Stay.Start, AllocatedEndTime = Stay.End }], null);
        Assert.Null(facts.CountAvailableCars([slot, other], Stay, Now, TimeSpan.Zero, default));
        Assert.Equal(1, facts.CountAvailableCars([other], Stay, Now, TimeSpan.Zero, default));
    }

    [Fact]
    public void CountRejectsDuplicateIdentitiesAndHonorsCancellation()
    {
        var slot = Guid.NewGuid();
        var facts = new ReservationAvailabilityFacts([], [], [], null);
        Assert.Throws<ArgumentException>(() => facts.CountAvailableCars([slot, slot], Stay, Now, TimeSpan.Zero, default));
        Assert.Throws<OperationCanceledException>(() => facts.CountAvailableCars([], Stay, Now, TimeSpan.Zero,
            new CancellationToken(canceled: true)));
    }
}
