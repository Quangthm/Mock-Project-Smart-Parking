using SmartParking.PaymentService.Application.Usecase.CalculateParkingFee;
using SmartParking.PaymentService.Domain.Pricing;

namespace SmartParking.PaymentService.Tests.Application;

public class CalculateParkingFeeQueryValidatorTests
{
    private readonly CalculateParkingFeeQueryValidator _validator = new();

    private static PricingPolicySnapshot CreateSamplePolicy() =>
        new(Guid.NewGuid(), "Sample Tariff", 1, PricingVehicleType.CAR, 15, new List<TimeBlockRule>
        {
            new("ALL_DAY", new TimeOnly(0, 0), new TimeOnly(0, 0), 20000L)
        });

    [Fact]
    public void Validate_WithValidCarRequest_PassesValidation()
    {
        var query = new CalculateParkingFeeQuery(
            TenantId: Guid.NewGuid(),
            SiteId: Guid.NewGuid(),
            VehicleType: "CAR",
            EntryTime: DateTimeOffset.UtcNow,
            ExitTime: DateTimeOffset.UtcNow.AddHours(1)
        );

        var result = _validator.Validate(query);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WithValidMotorcycleRequest_PassesValidation()
    {
        var query = new CalculateParkingFeeQuery(
            TenantId: Guid.NewGuid(),
            SiteId: Guid.NewGuid(),
            VehicleType: "MOTORCYCLE",
            EntryTime: DateTimeOffset.UtcNow,
            ExitTime: DateTimeOffset.UtcNow.AddHours(1)
        );

        var result = _validator.Validate(query);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WithExitTimeBeforeEntryTime_FailsValidation()
    {
        var query = new CalculateParkingFeeQuery(
            TenantId: Guid.NewGuid(),
            SiteId: Guid.NewGuid(),
            VehicleType: "CAR",
            EntryTime: DateTimeOffset.UtcNow,
            ExitTime: DateTimeOffset.UtcNow.AddHours(-1)
        );

        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "INVALID_TIME_RANGE");
    }

    [Theory]
    [InlineData("OVERSIZED")]
    [InlineData("TRUCK")]
    [InlineData("BUS")]
    [InlineData("BICYCLE")]
    public void Validate_WithUnsupportedVehicleType_FailsValidation(string vehicleType)
    {
        var query = new CalculateParkingFeeQuery(
            TenantId: Guid.NewGuid(),
            SiteId: Guid.NewGuid(),
            VehicleType: vehicleType,
            EntryTime: DateTimeOffset.UtcNow,
            ExitTime: DateTimeOffset.UtcNow.AddHours(1)
        );

        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "UNSUPPORTED_VEHICLE_TYPE");
    }

    [Fact]
    public void Validate_UntrustedCallerProvidingPolicyOverride_FailsValidation()
    {
        var query = new CalculateParkingFeeQuery(
            TenantId: Guid.NewGuid(),
            SiteId: Guid.NewGuid(),
            VehicleType: "CAR",
            EntryTime: DateTimeOffset.UtcNow,
            ExitTime: DateTimeOffset.UtcNow.AddHours(1),
            PolicyOverride: CreateSamplePolicy(),
            IsTrustedInternalCaller: false
        );

        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "FORBIDDEN");
    }

    [Fact]
    public void Validate_TrustedCallerProvidingPolicyOverride_PassesValidation()
    {
        var query = new CalculateParkingFeeQuery(
            TenantId: Guid.NewGuid(),
            SiteId: Guid.NewGuid(),
            VehicleType: "CAR",
            EntryTime: DateTimeOffset.UtcNow,
            ExitTime: DateTimeOffset.UtcNow.AddHours(1),
            PolicyOverride: CreateSamplePolicy(),
            IsTrustedInternalCaller: true
        );

        var result = _validator.Validate(query);

        Assert.True(result.IsValid);
    }
}
