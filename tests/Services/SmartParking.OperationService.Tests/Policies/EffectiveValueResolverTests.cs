using SmartParking.OperationService.Application.Policies.Services;
using SmartParking.OperationService.Domain.Policies;

namespace SmartParking.OperationService.Tests.Policies;

public class EffectiveValueResolverTests
{
    [Fact]
    public void Resolve_WhenConfiguredValueIsNull_ReturnsAdminDefault()
    {
        // Arrange
        int? configured = null;
        const int adminDefault = 15;

        // Act
        var result = EffectiveValueResolver.Resolve(configured, adminDefault);

        // Assert
        Assert.Equal(adminDefault, result);
    }

    [Fact]
    public void Resolve_WhenConfiguredValueIsNotNull_ReturnsConfiguredValue()
    {
        // Arrange
        int? configured = 25;
        const int adminDefault = 15;

        // Act
        var result = EffectiveValueResolver.Resolve(configured, adminDefault);

        // Assert
        Assert.Equal(25, result);
    }

    [Theory]
    [InlineData(10, 5, 30)]
    [InlineData(5, 5, 30)]
    [InlineData(30, 5, 30)]
    public void ValidateBounds_WhenValueWithinBounds_DoesNotThrow(int val, int min, int max)
    {
        // Act & Assert
        EffectiveValueResolver.ValidateBounds("testParam", val, min, max);
    }

    [Theory]
    [InlineData(4, 5, 30)]
    [InlineData(35, 5, 30)]
    public void ValidateBounds_WhenValueOutOfBounds_ThrowsPolicyOutOfBoundsException(int val, int min, int max)
    {
        // Act & Assert
        var ex = Assert.Throws<PolicyOutOfBoundsException>(() =>
            EffectiveValueResolver.ValidateBounds("paymentHoldDurationMinutes", val, min, max));

        Assert.Equal("paymentHoldDurationMinutes", ex.ParameterName);
        Assert.Contains("must be between", ex.Message);
    }

    [Fact]
    public void ValidateTimeLogic_WhenMinDurationExceedsMaxDuration_ThrowsInvalidPolicyDurationException()
    {
        // Arrange: 120 minutes > 1 hour (60 minutes)
        int minMinutes = 120;
        int maxHours = 1;

        // Act & Assert
        var ex = Assert.Throws<InvalidPolicyDurationException>(() =>
            EffectiveValueResolver.ValidateTimeLogic(minMinutes, maxHours));

        Assert.Contains("minReservationDurationMinutes cannot exceed maxReservationDurationHours", ex.Message);
    }

    [Fact]
    public void ValidateTimeLogic_WhenMinDurationLessThanOrEqualToMaxDuration_DoesNotThrow()
    {
        // Arrange: 60 minutes <= 2 hours (120 minutes)
        int minMinutes = 60;
        int maxHours = 2;

        // Act & Assert
        EffectiveValueResolver.ValidateTimeLogic(minMinutes, maxHours);
    }
}
