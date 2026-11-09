using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Tests;

public sealed class StructureBatchTests
{
    private static ParkingStructure Site()
        => new(new(Guid.NewGuid(), Guid.NewGuid(), "SITE", "Site", "Address", null, null, true, 0));

    [Fact]
    public void BatchIsAllOrNothingAndReportsOnlyFailingItems()
    {
        var site = Site(); var zone = site.AddUnit(UnitType.ZONE, "A"); var slot = site.AddSlot(zone, "01", VehicleType.CAR);
        var foreign = Guid.NewGuid();

        var error = Assert.Throws<StructureBatchException>(() => site.ApplyBatch([
            new(slot, "removeSlot"),
            new(foreign, "removeUnit")]));

        Assert.Equal("STRUCTURE_CONFLICT", error.Code);
        var failure = Assert.Single(error.Failures);
        Assert.Equal(foreign, failure.ResourceId);
        Assert.Equal("NOT_FOUND", failure.Code);   // foreign ids never leak details
        Assert.Contains("not part of this lot", failure.Message);
        // The first item was rolled back: nothing from the batch was applied.
        Assert.Equal(slot, Assert.Single(site.Slots).Id);
        Assert.Single(site.Units);
        Assert.Equal(1, site.Site.TotalPhysicalCapacity);
    }

    [Fact]
    public void BatchAppliesEveryItemWhenAllSucceed()
    {
        var site = Site(); var zone = site.AddUnit(UnitType.ZONE, "A"); var slot = site.AddSlot(zone, "01", VehicleType.CAR);

        site.ApplyBatch([new(slot, "removeSlot"), new(zone, "removeUnit"), new(site.Site.Id, "active")]);

        Assert.Empty(site.Slots);
        Assert.Empty(site.Units);
        Assert.False(site.Site.IsActive);
    }

    [Fact]
    public void BatchRollsBackWhenALaterItemFailsDuringApplication()
    {
        var site = Site(); var zone = site.AddUnit(UnitType.ZONE, "A");
        var first = site.AddSlot(zone, "01", VehicleType.CAR); site.AddSlot(zone, "02", VehicleType.CAR);

        var error = Assert.Throws<StructureBatchException>(() => site.ApplyBatch([
            new(first, "removeSlot"),
            new(zone, "removeUnit")]));   // the unit still holds slot 02

        Assert.Equal("UNIT_NOT_EMPTY", Assert.Single(error.Failures).Code);
        Assert.Equal(2, site.Slots.Count);
        Assert.Contains(site.Slots, candidate => candidate.Id == first);
        Assert.Equal(2, site.Site.TotalPhysicalCapacity);
    }

    [Fact]
    public void BatchRejectsUnsupportedActionsAndWrongSizes()
    {
        var site = Site();
        Assert.Equal("INVALID_STRUCTURE",
            Assert.Throws<StructureException>(() => site.ApplyBatch([new(Guid.NewGuid(), "profile")])).Code);
        Assert.Equal("INVALID_STRUCTURE", Assert.Throws<StructureException>(() => site.ApplyBatch([])).Code);
        var tooMany = Enumerable.Range(0, 51).Select(_ => new StructureBatchItem(Guid.NewGuid(), "removeSlot")).ToArray();
        Assert.Equal("INVALID_STRUCTURE", Assert.Throws<StructureException>(() => site.ApplyBatch(tooMany)).Code);
    }

    [Fact]
    public void BatchRechecksTheVersionTheClientSaw()
    {
        var site = Site(); var zone = site.AddUnit(UnitType.ZONE, "A"); var slot = site.AddSlot(zone, "01", VehicleType.CAR);
        var structure = new ParkingStructure(site.Site, site.Units, site.Slots,
            versions: new Dictionary<Guid, DateTime> { [slot] = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc) });

        var error = Assert.Throws<StructureBatchException>(() => structure.ApplyBatch([
            new(slot, "removeSlot", new DateTime(2026, 10, 29, 11, 0, 0, DateTimeKind.Utc))]));

        Assert.Equal("STRUCTURE_CONFLICT", Assert.Single(error.Failures).Code);
        Assert.Single(structure.Slots);
    }
}
