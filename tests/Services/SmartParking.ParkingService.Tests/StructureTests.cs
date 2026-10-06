using SmartParking.ParkingService.Domain;

namespace SmartParking.ParkingService.Tests;

public sealed class StructureTests
{
    private static ParkingStructure Site() => new(new(Guid.NewGuid(), Guid.NewGuid(), "SITE", "Site", "Address", null, null, true, 0));
    private static void Error(string code, Action action) => Assert.Equal(code, Assert.Throws<StructureException>(action).Code);

    [Fact]
    public void OutdoorZoneNeedsNoFloorAndMotorcyclesHaveIndependentSlots()
    {
        var site = Site(); var zone = site.AddUnit(UnitType.ZONE, "Outdoor", maxCapacity: 2);
        site.AddSlot(zone, " M01 ", VehicleType.MOTORBIKE);
        site.AddSlot(zone, "C01", VehicleType.SEDAN);
        Assert.Equal(2, site.Site.TotalPhysicalCapacity);
        Assert.Equal("M01", site.Slots[0].Code);
        Error("CAPACITY_EXCEEDED", () => site.AddSlot(zone, "C02", VehicleType.SEDAN));
        Error("DUPLICATE_IDENTIFIER", () => site.AddSlot(zone, "m01", VehicleType.MOTORBIKE));
        Assert.Equal(2, site.Slots.Count);
    }
    [Fact]
    public void ParentCapacityCoversAllDescendantsAndInvalidParentDoesNotMutate()
    {
        var site = Site(); var floor = site.AddUnit(UnitType.FLOOR, "B1", maxCapacity: 1);
        var zoneA = site.AddUnit(UnitType.ZONE, "A", floor); var zoneB = site.AddUnit(UnitType.ZONE, "B", floor);
        site.AddSlot(zoneA, "01", VehicleType.SUV);
        Error("CAPACITY_EXCEEDED", () => site.AddSlot(zoneB, "01", VehicleType.MOTORBIKE));
        Error("INVALID_STRUCTURE", () => site.AddUnit(UnitType.FLOOR, "B2", zoneA));
        Error("UNIT_NOT_FOUND", () => site.AddUnit(UnitType.ZONE, "foreign", Guid.NewGuid()));
        Assert.Equal(3, site.Units.Count);
        Error("DUPLICATE_IDENTIFIER", () => site.AddUnit(UnitType.ZONE, " a ", floor));
        Assert.StartsWith(site.Units[0].Path, site.Units[1].Path);
    }
    [Theory]
    [InlineData(true, false, false, OperationalStatus.OPERATIONAL)]
    [InlineData(false, true, false, OperationalStatus.OPERATIONAL)]
    [InlineData(false, false, true, OperationalStatus.OPERATIONAL)]
    [InlineData(false, false, false, OperationalStatus.UNKNOWN)]
    public void ProtectedResourcesCannotBeMovedRemovedOrDeactivated(bool occupied, bool protectedSlot, bool commitments, OperationalStatus status)
    {
        var site = Site(); var zone = site.AddUnit(UnitType.ZONE, "A"); var other = site.AddUnit(UnitType.ZONE, "B");
        var id = site.AddSlot(zone, "01", VehicleType.SEDAN);
        var guarded = new ParkingStructure(site.Site, site.Units, [site.Slots[0] with { IsPhysicallyOccupied = occupied, OperationalStatus = status }],
            protectedSlots: protectedSlot ? [id] : [], hasLiveCommitments: commitments);
        Error("STRUCTURE_IN_USE", () => guarded.RemoveSlot(id));
        Error("STRUCTURE_IN_USE", () => guarded.MoveSlot(id, other, "02"));
        Error("STRUCTURE_IN_USE", () => guarded.SetActive(false));
        Assert.Equal(zone, guarded.Slots.Single().UnitId); Assert.True(guarded.Site.IsActive);
        // Adding physical capacity does not displace occupancy or cancel commitments.
        guarded.AddSlot(other, "02", VehicleType.MOTORBIKE);
        Assert.Equal(2, guarded.Site.TotalPhysicalCapacity);
    }
    [Fact]
    public void EmptyUnitRemovalPreservesReferencesAndSlotMovePreservesIdentity()
    {
        var site = Site(); var a = site.AddUnit(UnitType.ZONE, "A"); var b = site.AddUnit(UnitType.ZONE, "B");
        var slot = site.AddSlot(a, "01", VehicleType.SEDAN);
        var path = site.AddAccessPath("entry", null, a, "{\"points\":[[0,0],[1,1]]}");
        Error("UNIT_NOT_EMPTY", () => site.RemoveUnit(a));
        site.MoveSlot(slot, b, "02"); Assert.Equal(slot, site.Slots.Single().Id);
        Error("UNIT_NOT_EMPTY", () => site.RemoveUnit(a));
        site.RemoveAccessPath(path); site.RemoveUnit(a);
        site.RemoveSlot(slot); Assert.Equal(0, site.Site.TotalPhysicalCapacity);
    }
    [Theory]
    [InlineData(91, 0)] [InlineData(0, 181)]
    public void InvalidCoordinatesDoNotChangeProfile(int latitude, int longitude)
    {
        var site = Site(); Error("INVALID_STRUCTURE", () => site.UpdateProfile("site", "New", "Address", latitude, longitude));
        Assert.Equal("Site", site.Site.Name);
    }
    [Fact]
    public void InvalidMapAndCapacityAreRejected()
    {
        var site = Site(); var a = site.AddUnit(UnitType.ZONE, "A", maxCapacity: 2);
        site.AddSlot(a, "01", VehicleType.SEDAN); site.AddSlot(a, "02", VehicleType.SEDAN);
        Error("INVALID_STRUCTURE", () => site.SetUnitCapacity(a, 1));
        Error("INVALID_STRUCTURE", () => site.AddAccessPath("X", null, a, "invalid"));
        Error("INVALID_STRUCTURE", () => site.AddSlot(a, "03", (VehicleType)999));
        Assert.Empty(site.Paths);
    }
}
