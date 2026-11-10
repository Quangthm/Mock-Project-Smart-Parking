using SmartParking.FacilityService.Application;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Tests;

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
    public void CompatibilitySetIsAPredicateAndThePrimaryCategoryIsNeverInferred()
    {
        var site = Site(); var zone = site.AddUnit(UnitType.ZONE, "A");
        var id = site.AddSlot(zone, "01", VehicleType.CAR, vehicleTypes: [VehicleType.CAR, VehicleType.MOTORCYCLE]);

        var slot = site.Slots.Single();
        Assert.Equal(VehicleType.CAR, slot.VehicleType);
        Assert.Equal(new[] { VehicleType.CAR, VehicleType.MOTORCYCLE }, slot.VehicleTypes);
        // Compatibility never multiplies stock: the slot is counted once, in its primary pool.
        Assert.Equal(1, site.Site.TotalPhysicalCapacity);
        Assert.Equal(1, site.CapacityViews.Single(v => v.UnitId is null && v.VehicleType == VehicleType.CAR).Total);
        Assert.Equal(0, site.CapacityViews.Single(v => v.UnitId is null && v.VehicleType == VehicleType.MOTORCYCLE).Total);

        Error("INVALID_STRUCTURE", () => site.AddSlot(zone, "02", VehicleType.CAR, vehicleTypes: []));
        Error("INVALID_STRUCTURE", () => site.ConfigureSlot(id, VehicleType.MOTORCYCLE, SlotType.STANDARD, null, null, [VehicleType.CAR]));
        Assert.Single(site.Slots);
    }
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
    public void OutcomeArraysAreCanonicallyOrderedForStableJsonbComparison()
    {
        var site = Site();
        var zoneA = site.AddUnit(UnitType.ZONE, "A");
        var zoneB = site.AddUnit(UnitType.ZONE, "B");
        var zoneC = site.AddUnit(UnitType.ZONE, "C");
        var slotA = site.AddSlot(zoneA, "A01", VehicleType.CAR);
        var slotB = site.AddSlot(zoneB, "B01", VehicleType.CAR);
        var slotC = site.AddSlot(zoneC, "C01", VehicleType.MOTORCYCLE);

        // Caller-supplied "original" sets are unordered; the builder must still emit a stable order
        // because Reservation compares the persisted receipt with `outcome=$4::jsonb`.
        var outcome = StructureOutcomeBuilder.Build(
            site,
            new HashSet<Guid> { slotC, slotA, slotB },
            new HashSet<Guid> { zoneC, zoneA, zoneB });

        Assert.Equal(outcome.RemovedSlots, outcome.RemovedSlots.OrderBy(id => id).ToArray());
        Assert.Equal(outcome.RemovedUnits, outcome.RemovedUnits.OrderBy(id => id).ToArray());
        Assert.Equal(outcome.Capacities,
            outcome.Capacities.OrderBy(c => c.UnitId).ThenBy(c => c.VehicleType, StringComparer.Ordinal).ToArray());
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
    [Fact]
    public void RestoreSlotIsIdempotentKeepsTheIdAndIsCountedOnce()
    {
        var site = Site(); var zone = site.AddUnit(UnitType.ZONE, "A");
        var released = new ParkingSlot(Guid.NewGuid(), zone, "A01", VehicleType.CAR, SlotType.STANDARD)
        { VehicleTypes = [VehicleType.CAR, VehicleType.MOTORCYCLE] };
        var structure = new ParkingStructure(site.Site, site.Units, releasedSlots: [released]);

        structure.RestoreSlot(released.Id);
        structure.RestoreSlot(released.Id);

        var slot = Assert.Single(structure.Slots);
        Assert.Equal(released.Id, slot.Id);
        Assert.Equal(OperationalStatus.OPERATIONAL, slot.OperationalStatus);
        Assert.Equal(new[] { VehicleType.CAR, VehicleType.MOTORCYCLE }, slot.VehicleTypes);
        Assert.Empty(structure.ReleasedSlots);
        Assert.Equal(1, structure.Site.TotalPhysicalCapacity);
        Assert.Equal(1, structure.CapacityViews.Single(v => v.UnitId is null && v.VehicleType == VehicleType.CAR).Total);
        Assert.Equal(0, structure.CapacityViews.Single(v => v.UnitId is null && v.VehicleType == VehicleType.MOTORCYCLE).Total);
    }
    [Fact]
    public void RestoringASlotWhoseUnitIsStillReleasedRequiresTheUnitFirst()
    {
        var site = Site();
        var unitId = Guid.NewGuid();
        var unit = new SpatialUnit(unitId, null, $"/{site.Site.Id}/{unitId}/", UnitType.ZONE, "A", 0);
        var released = new ParkingSlot(Guid.NewGuid(), unitId, "A01", VehicleType.CAR, SlotType.STANDARD);
        var structure = new ParkingStructure(site.Site, releasedSlots: [released], releasedUnits: [unit]);

        Error("UNIT_NOT_FOUND", () => structure.RestoreSlot(released.Id));
        Assert.Empty(structure.Slots);
        Assert.Single(structure.ReleasedSlots);
        Assert.Equal(0, structure.Site.TotalPhysicalCapacity);
    }
    [Fact]
    public void RestoringAUnitWhoseNameIsHeldByALiveSiblingIsRejected()
    {
        var site = Site(); site.AddUnit(UnitType.ZONE, "A");
        var unitId = Guid.NewGuid();
        var released = new SpatialUnit(unitId, null, $"/{site.Site.Id}/{unitId}/", UnitType.ZONE, "a", 0);
        var structure = new ParkingStructure(site.Site, site.Units, releasedUnits: [released]);

        Error("DUPLICATE_IDENTIFIER", () => structure.RestoreUnit(unitId));
        Assert.Single(structure.Units);
        Assert.Single(structure.ReleasedUnits);
    }
    [Fact]
    public void RestoringAPathWhoseEndpointIsReleasedIsRejected()
    {
        var site = Site(); var live = site.AddUnit(UnitType.ZONE, "A");
        var releasedUnitId = Guid.NewGuid();
        var releasedUnit = new SpatialUnit(releasedUnitId, null, $"/{site.Site.Id}/{releasedUnitId}/", UnitType.ZONE, "B", 0);
        var releasedPath = new AccessPath(Guid.NewGuid(), "P1", live, releasedUnitId, null);
        var structure = new ParkingStructure(site.Site, site.Units, releasedPaths: [releasedPath], releasedUnits: [releasedUnit]);

        Error("UNIT_NOT_FOUND", () => structure.RestorePath(releasedPath.Id));
        Assert.Empty(structure.Paths);
        Assert.Single(structure.ReleasedPaths);
    }
    [Fact]
    public void RestoringAnUnknownIdReportsTheMatchingNotFoundCode()
    {
        var site = Site();
        Error("SLOT_NOT_FOUND", () => site.RestoreSlot(Guid.NewGuid()));
        Error("UNIT_NOT_FOUND", () => site.RestoreUnit(Guid.NewGuid()));
        Error("PATH_NOT_FOUND", () => site.RestorePath(Guid.NewGuid()));
    }
    [Fact]
    public void RestoreUnitAndPathKeepTheirOriginalIdentity()
    {
        var site = Site();
        var unitId = Guid.NewGuid();
        var releasedUnit = new SpatialUnit(unitId, null, $"/{site.Site.Id}/{unitId}/", UnitType.ZONE, "A", 0);
        var releasedPath = new AccessPath(Guid.NewGuid(), "P1", unitId, null, "{}");
        var structure = new ParkingStructure(site.Site, releasedUnits: [releasedUnit], releasedPaths: [releasedPath]);

        structure.RestoreUnit(unitId);
        structure.RestorePath(releasedPath.Id);
        structure.RestoreUnit(unitId);
        structure.RestorePath(releasedPath.Id);

        Assert.Equal(unitId, structure.Units.Single().Id);
        Assert.Equal(releasedUnit.Path, structure.Units.Single().Path);
        Assert.Equal(releasedPath.Id, structure.Paths.Single().Id);
        Assert.Empty(structure.ReleasedUnits);
        Assert.Empty(structure.ReleasedPaths);
    }
}
