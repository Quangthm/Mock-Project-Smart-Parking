using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Tests;

// The site's model-derived ceiling is a domain invariant: no persistence or transport path may
// create more FLOOR units than SiteProfile.MaxFloors allows.
public sealed class ParkingStructureMaxFloorsTests
{
    [Fact]
    public void AddUnit_FloorBeyondMaxFloors_ThrowsMaxFloorsExceeded()
    {
        var structure = Structure(maxFloors: 2);

        structure.AddUnit(UnitType.FLOOR, "Level 1");
        structure.AddUnit(UnitType.FLOOR, "Level 2");

        var error = Assert.Throws<StructureException>(() => structure.AddUnit(UnitType.FLOOR, "Level 3"));

        Assert.Equal("MAX_FLOORS_EXCEEDED", error.Code);
        Assert.Equal("This parking model supports at most 2 floor(s).", error.Message);
        Assert.Equal(2, structure.Units.Count(unit => unit.Type == UnitType.FLOOR));
    }

    [Fact]
    public void AddUnit_WhenMaxFloorsIsNull_AllowsFloors()
    {
        var structure = Structure(maxFloors: null);

        for (var level = 1; level <= 5; level++)
            structure.AddUnit(UnitType.FLOOR, $"Level {level}");

        Assert.Equal(5, structure.Units.Count(unit => unit.Type == UnitType.FLOOR));
    }

    [Fact]
    public void AddUnit_FloorBelowMaxFloors_Succeeds()
    {
        var structure = Structure(maxFloors: 3);

        structure.AddUnit(UnitType.FLOOR, "Level 1");
        structure.AddUnit(UnitType.FLOOR, "Level 2");

        Assert.Equal(2, structure.Units.Count(unit => unit.Type == UnitType.FLOOR));
    }

    [Fact]
    public void AddUnit_NonFloorUnits_AreNotBoundedByMaxFloors()
    {
        var structure = Structure(maxFloors: 1);

        structure.AddUnit(UnitType.FLOOR, "Level 1");
        structure.AddUnit(UnitType.ZONE, "Zone A");

        Assert.Equal(1, structure.Units.Count(unit => unit.Type == UnitType.FLOOR));
        Assert.Equal(1, structure.Units.Count(unit => unit.Type == UnitType.ZONE));
    }

    [Theory]
    [InlineData("outdoor", "outdoor_parking_lot.glb", 1)]
    [InlineData(null, "outdoor_parking_lot_v2.glb", 1)]
    [InlineData("basement", "underground_parking_lot.glb", 2)]
    [InlineData("multi-storey", "indoor_parking_lot.glb", 3)]
    [InlineData("outdoor", null, 1)]
    [InlineData("basement", null, 2)]
    [InlineData("multi-storey", null, 3)]
    public void Resolve_KnownTemplates_ReturnsCeiling(string? lotType, string? modelFileName, int expected)
        => Assert.Equal(expected, ParkingModelFloorCeilings.Resolve(lotType, modelFileName));

    [Theory]
    [InlineData("multi-storey", "custom-4f.glb")]
    [InlineData("basement", "my_custom_garage.glb")]
    public void Resolve_CustomModelForNonOutdoor_ReturnsNull(string lotType, string modelFileName)
        => Assert.Null(ParkingModelFloorCeilings.Resolve(lotType, modelFileName));

    [Fact]
    public void Resolve_CustomModelForOutdoor_StillReturnsOne()
        => Assert.Equal(1, ParkingModelFloorCeilings.Resolve("outdoor", "custom-outdoor.glb"));

    private static ParkingStructure Structure(int? maxFloors)
        => new(new SiteProfile(Guid.NewGuid(), Guid.NewGuid(), "TEST", "Test", "Address",
            null, null, true, 0, MaxFloors: maxFloors));
}
