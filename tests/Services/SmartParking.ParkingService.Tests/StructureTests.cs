using SmartParking.ParkingService.Domain;

namespace SmartParking.ParkingService.Tests;

public sealed class StructureTests
{
    [Fact]
    public void BackupPhysicalOverlapAndEmptyUnitCannotCreateInvalidCapacity()
    {
        var site=Site();var zone=site.AddUnit(UnitType.ZONE,"Occupied");var empty=site.AddUnit(UnitType.ZONE,"Empty");
        site.AddSlot(zone,"C1",VehicleType.CAR);
        Error("CAPACITY_CONFLICT",()=>site.ConfigureBackup(empty,VehicleType.CAR,1));Assert.Empty(site.BackupPolicies);
        foreach(var state in new[]{OperationalStatus.OPERATIONAL,OperationalStatus.MAINTENANCE})
        {
            var slot=site.Slots.Single() with{ReservationState="BACKUP",OperationalStatus=state,IsPhysicallyOccupied=state==OperationalStatus.OPERATIONAL};
            var view=new ParkingStructure(site.Site,site.Units,[slot]).CapacityViews.Single(v=>v.UnitId==null && v.VehicleType==VehicleType.CAR);
            Assert.Equal(0,view.Available);Assert.Equal(0,view.EffectiveBackup);Assert.Equal(1,view.MarkedBackup);
        }
    }
    private static ParkingStructure Site() => new(new(Guid.NewGuid(), Guid.NewGuid(), "SITE", "Site", "Address", null, null, true, 0));
    private static void Error(string code, Action action) => Assert.Equal(code, Assert.Throws<StructureException>(action).Code);

    [Fact]
    public void OutdoorZoneNeedsNoFloorAndMotorcyclesHaveIndependentSlots()
    {
        var site = Site(); var zone = site.AddUnit(UnitType.ZONE, "Outdoor", maxCapacity: 2);
        site.AddSlot(zone, " M01 ", VehicleType.MOTORCYCLE);
        site.AddSlot(zone, "C01", VehicleType.CAR);
        Assert.Equal(2, site.Site.TotalPhysicalCapacity);
        Assert.Equal("M01", site.Slots[0].Code);
        Error("CAPACITY_EXCEEDED", () => site.AddSlot(zone, "C02", VehicleType.CAR));
        Error("DUPLICATE_IDENTIFIER", () => site.AddSlot(zone, "m01", VehicleType.MOTORCYCLE));
        Assert.Equal(2, site.Slots.Count);
    }
    [Fact]
    public void ParentCapacityCoversAllDescendantsAndInvalidParentDoesNotMutate()
    {
        var site = Site(); var floor = site.AddUnit(UnitType.FLOOR, "B1", maxCapacity: 1);
        var zoneA = site.AddUnit(UnitType.ZONE, "A", floor); var zoneB = site.AddUnit(UnitType.ZONE, "B", floor);
        site.AddSlot(zoneA, "01", VehicleType.CAR);
        Error("CAPACITY_EXCEEDED", () => site.AddSlot(zoneB, "02", VehicleType.MOTORCYCLE));
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
        var id = site.AddSlot(zone, "01", VehicleType.CAR);
        var guarded = new ParkingStructure(site.Site, site.Units, [site.Slots[0] with { IsPhysicallyOccupied = occupied, OperationalStatus = status }],
            protectedSlots: protectedSlot ? [id] : [], hasLiveCommitments: commitments);
        Error("STRUCTURE_IN_USE", () => guarded.RemoveSlot(id));
        Error("STRUCTURE_IN_USE", () => guarded.MoveSlot(id, other, "02"));
        Error("STRUCTURE_IN_USE", () => guarded.SetActive(false));
        Assert.Equal(zone, guarded.Slots.Single().UnitId); Assert.True(guarded.Site.IsActive);
        // Adding physical capacity does not displace occupancy or cancel commitments.
        guarded.AddSlot(other, "02", VehicleType.MOTORCYCLE);
        Assert.Equal(2, guarded.Site.TotalPhysicalCapacity);
    }
    [Fact]
    public void EmptyUnitRemovalPreservesReferencesAndSlotMovePreservesIdentity()
    {
        var site = Site(); var a = site.AddUnit(UnitType.ZONE, "A"); var b = site.AddUnit(UnitType.ZONE, "B");
        var slot = site.AddSlot(a, "01", VehicleType.CAR);
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
        site.AddSlot(a, "01", VehicleType.CAR); site.AddSlot(a, "02", VehicleType.CAR);
        Error("INVALID_STRUCTURE", () => site.SetUnitCapacity(a, 1));
        Error("INVALID_STRUCTURE", () => site.AddAccessPath("X", null, a, "invalid"));
        Error("INVALID_STRUCTURE", () => site.AddSlot(a, "03", (VehicleType)999));
        Assert.Empty(site.Paths);
    }
    [Fact]
    public void BackupOverlapIsCountedOnceAndPhysicalStateCannotBeOverwritten()
    {
        var site=Site();var zone=site.AddUnit(UnitType.ZONE,"A");var a=site.AddSlot(zone,"A",VehicleType.CAR);site.AddSlot(zone,"B",VehicleType.CAR);
        site.ConfigureBackup(null,VehicleType.CAR,1);site.MarkBackup(a,true);
        var capacity=site.CapacityViews.Single(v=>v.UnitId==null && v.VehicleType==VehicleType.CAR);
        Assert.Equal(1,capacity.ConfiguredBackup);Assert.Equal(1,capacity.MarkedBackup);Assert.Equal(1,capacity.EffectiveBackup);Assert.Equal(1,capacity.Available);
        Error("INVALID_STRUCTURE",()=>site.ConfigureBackup(zone,VehicleType.CAR,1));
        Error("CAPACITY_CONFLICT",()=>site.ConfigureBackup(null,VehicleType.CAR,3));Assert.Equal(1,site.BackupPolicies.Single().Count);
        var occupied=new ParkingStructure(site.Site,site.Units,site.Slots.Select(s=>s.Id==a?s with{IsPhysicallyOccupied=true}:s));
        Error("STRUCTURE_IN_USE",()=>occupied.MarkBackup(a,true));Assert.True(occupied.Slots.Single(s=>s.Id==a).IsPhysicallyOccupied);
    }
    [Fact]
    public void CapacityDimensionsIncludePendingHoldsAndDetectInconsistentCounts()
    {
        var site=Site();var zone=site.AddUnit(UnitType.ZONE,"A");site.AddSlot(zone,"C1",VehicleType.CAR);site.AddSlot(zone,"C2",VehicleType.CAR);site.AddSlot(zone,"M1",VehicleType.MOTORCYCLE);
        var pending=new ParkingStructure(site.Site,site.Units,site.Slots,claims:[new(null,"CAR",1,0)]);
        Assert.Equal(1,pending.CapacityViews.Single(v=>v.UnitId==null && v.VehicleType==VehicleType.CAR).Available);
        Assert.Equal(1,pending.CapacityViews.Single(v=>v.UnitId==null && v.VehicleType==VehicleType.MOTORCYCLE).Available);
        var invalid=new ParkingStructure(site.Site,site.Units,site.Slots,claims:[new(null,"CAR",3,0)]);
        Error("CAPACITY_CONFLICT",()=>invalid.ValidateCapacity());
    }
}
