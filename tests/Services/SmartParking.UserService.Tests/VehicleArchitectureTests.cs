using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.UserService.Domain.Entities;
using UserService.Application.Common.MapperProfile;
using UserService.Application.Usecase.Vehicles;

namespace SmartParking.UserService.Tests;

public sealed class VehicleArchitectureTests
{
    [Fact]
    public void VehicleMapsExposeOnlyManagedContentAndKeepRawInput()
    {
        var configuration = new MapperConfiguration(c => c.AddProfile<VehicleMapperProfile>(), NullLoggerFactory.Instance);
        configuration.AssertConfigurationIsValid();
        var mapper = configuration.CreateMapper();
        var request = new SaveVehicleRequest { Plate = " 51a-123.45 ", VehicleType = "CAR", ImageAssetId = Guid.NewGuid() };
        var command = mapper.Map<SaveOwnVehicleCommand>(request);
        Assert.Null(command.VehicleId);
        Assert.Equal(request.Plate, command.Plate);
        var vehicle = RegisteredVehicle.Register(Guid.NewGuid(), request.Plate, request.VehicleType, DateTimeOffset.UtcNow);
        vehicle.ImageReference = "https://legacy.example.test/not-authoritative.jpg";
        var legacy = mapper.Map<VehicleResponse>(vehicle);
        Assert.Null(legacy.ImageReference);
        Assert.Equal("reupload-required", legacy.ImageStatus);
        vehicle.UpdateMetadata(request.Plate, request.ImageAssetId!.Value, DateTimeOffset.UtcNow);
        var managed = mapper.Map<VehicleResponse>(vehicle);
        Assert.Equal($"/api/media/images/{request.ImageAssetId}/content", managed.ImageReference);
        Assert.Equal("managed", managed.ImageStatus);
    }

    [Fact]
    public void MetadataCannotChangeIdentityAndClaimCannotGrantAnActiveBinding()
    {
        var now = DateTimeOffset.UtcNow;
        var account = Guid.NewGuid();
        var vehicle = RegisteredVehicle.Register(account, " 51a-123.45 ", "CAR", now);
        var originalId = vehicle.Id;
        vehicle.UpdateMetadata("51-A-12345", Guid.NewGuid(), now.AddMinutes(1));
        Assert.Equal(originalId, vehicle.Id);
        Assert.Equal("51A12345", vehicle.CanonicalPlate);
        Assert.Equal(1, vehicle.BindingRevision);
        Assert.Throws<InvalidOperationException>(() => vehicle.UpdateMetadata("51B12345", Guid.NewGuid(), now));
        var claim = VehicleRegistrationClaim.Submit(Guid.NewGuid(), "51A12345", "CAR", Guid.NewGuid(), now);
        Assert.Equal("PENDING_DISPUTE", claim.Status);
        Assert.NotEqual(account, claim.AccountId);
        Assert.NotEqual(vehicle.Id, claim.Id);
        Assert.Equal(account, vehicle.AccountId);
    }

    [Fact]
    public async Task VehicleValidatorsRejectMalformedCommandsBeforePersistence()
    {
        var save = new SaveOwnVehicleValidator();
        Assert.False((await save.ValidateAsync(new SaveOwnVehicleCommand(null, "51A12/345", "CAR", Guid.NewGuid(), null))).IsValid);
        Assert.False((await save.ValidateAsync(new SaveOwnVehicleCommand(null, "51A12345", "CAR", null, null))).IsValid);
        Assert.True((await save.ValidateAsync(new SaveOwnVehicleCommand(null, " 51a-123.45 ", "CAR", Guid.NewGuid(), null))).IsValid);
        var lookup = new ResolveVehicleBindingValidator();
        Assert.False((await lookup.ValidateAsync(new ResolveVehicleBindingQuery(Guid.Empty, Guid.NewGuid(), null))).IsValid);
        Assert.False((await lookup.ValidateAsync(new ResolveVehicleBindingQuery(Guid.NewGuid(), Guid.NewGuid(), 0))).IsValid);
    }
}
