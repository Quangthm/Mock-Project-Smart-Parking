using System.ComponentModel.DataAnnotations;
using UserService.Application.DTOs;

namespace SmartParking.UserService.Tests;

// Lot-type floor ceilings enforced by OwnerLotSetupDto.Validate. Each DTO supplies the model file
// that matches its lot type so the model-file rule cannot mask the floor rule under test.
public sealed class OwnerLotSetupDtoValidationTests
{
    [Fact]
    public void Basement_ThreeFloors_IsRejected()
    {
        var results = Validate(Lot("basement", 3));

        Assert.Contains(results, result => result.ErrorMessage == "Basement lots support at most 2 floors.");
    }

    [Fact]
    public void MultiStorey_FourFloors_IsRejected()
    {
        var results = Validate(Lot("multi-storey", 4));

        Assert.Contains(results, result => result.ErrorMessage == "Multi-storey lots support at most 3 floors.");
    }

    [Fact]
    public void Outdoor_OneFloor_IsNotRejectedByFloorRules()
    {
        var results = Validate(Lot("outdoor", 1));

        Assert.DoesNotContain(results, result => result.ErrorMessage == "Outdoor lots must have one floor.");
    }

    [Fact]
    public void Basement_TwoFloors_IsAccepted()
    {
        var results = Validate(Lot("basement", 2));

        Assert.DoesNotContain(results, result => result.ErrorMessage == "Basement lots support at most 2 floors.");
    }

    [Fact]
    public void MultiStorey_ThreeFloors_IsAccepted()
    {
        var results = Validate(Lot("multi-storey", 3));

        Assert.DoesNotContain(results, result => result.ErrorMessage == "Multi-storey lots support at most 3 floors.");
    }

    [Fact]
    public void Outdoor_TwoFloors_IsRejected()
    {
        var results = Validate(Lot("outdoor", 2));

        Assert.Contains(results, result => result.ErrorMessage == "Outdoor lots must have one floor.");
    }

    private static ValidationResult[] Validate(OwnerLotSetupDto dto)
        => dto.Validate(new ValidationContext(dto)).ToArray();

    private static OwnerLotSetupDto Lot(string lotType, int floors) => new()
    {
        Name = "Lot",
        Address = "Address",
        LotType = lotType,
        Floors = floors,
        ModelFileName = lotType switch
        {
            "outdoor" => "outdoor_parking_lot.glb",
            "basement" => "underground_parking_lot.glb",
            _ => "indoor_parking_lot.glb"
        }
    };
}
