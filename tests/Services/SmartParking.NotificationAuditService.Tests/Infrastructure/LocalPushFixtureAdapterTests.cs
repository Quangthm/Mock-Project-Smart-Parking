using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.NotificationAuditService.Domain.Enums;
using SmartParking.NotificationAuditService.Infrastructure.Adapters.Delivery;

namespace SmartParking.NotificationAuditService.Tests.Infrastructure;

public class LocalPushFixtureAdapterTests
{
    [Fact]
    public async Task SendAsync_WhenCalled_ShouldReturnSuccessWithPushMockProvider()
    {
        // Arrange
        var logger = NullLogger<LocalPushFixtureAdapter>.Instance;
        var adapter = new LocalPushFixtureAdapter(logger);
        var notificationId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();

        // Act
        var result = await adapter.SendAsync(
            notificationId,
            recipientId,
            "Booking Confirmed",
            "Your parking space is ready.",
            "{\"slotId\":\"A-01\"}");

        // Assert
        Assert.Equal(NotificationChannel.PUSH, adapter.Channel);
        Assert.True(result.Success);
        Assert.Equal("LOCAL_PUSH_FIXTURE", result.DeliveryProvider);
        Assert.Null(result.ErrorMessage);
    }
}
