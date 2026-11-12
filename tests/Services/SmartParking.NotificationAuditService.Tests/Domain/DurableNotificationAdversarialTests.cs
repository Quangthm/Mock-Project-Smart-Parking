using System;
using System.Linq;
using System.Threading.Tasks;
using SmartParking.NotificationAuditService.Application.DTOs.Notifications;
using SmartParking.NotificationAuditService.Domain.Entities;
using SmartParking.NotificationAuditService.Domain.Enums;
using SmartParking.NotificationAuditService.Domain.Exceptions;
using Xunit;

namespace SmartParking.NotificationAuditService.Tests.Domain;

public class DurableNotificationAdversarialTests
{
    [Fact]
    public void MarkAsRead_CalledRepeatedly_ShouldMaintainStrictIdempotency()
    {
        // Arrange
        var notification = DurableNotification.Create(
            "PaymentSucceeded",
            "IDEMP-STRESS-1",
            Guid.NewGuid(),
            NotificationChannel.IN_APP,
            "TPL_01",
            "{}",
            title: "Idempotency Test",
            message: "Testing repeat MarkAsRead");

        Assert.False(notification.IsRead);
        Assert.Null(notification.ReadAt);

        // Act - Call 1
        var beforeFirst = DateTimeOffset.UtcNow;
        notification.MarkAsRead();
        var afterFirst = DateTimeOffset.UtcNow;

        var firstReadAt = notification.ReadAt;
        var firstUpdatedAt = notification.UpdatedAt;

        Assert.True(notification.IsRead);
        Assert.NotNull(firstReadAt);
        Assert.InRange(firstReadAt.Value, beforeFirst, afterFirst);
        Assert.InRange(firstUpdatedAt, beforeFirst, afterFirst);

        // Act - Repeat 25 times with small delays
        for (int i = 0; i < 25; i++)
        {
            notification.MarkAsRead();
            Assert.True(notification.IsRead);
            Assert.Equal(firstReadAt, notification.ReadAt);
            Assert.Equal(firstUpdatedAt, notification.UpdatedAt);
        }
    }

    [Fact]
    public void MarkAsRead_ConcurrentCalls_ShouldRemainConsistent()
    {
        // Arrange
        var notification = DurableNotification.Create(
            "PaymentSucceeded",
            "IDEMP-CONCUR-1",
            Guid.NewGuid(),
            NotificationChannel.IN_APP,
            "TPL_01",
            "{}");

        // Act - Invoke MarkAsRead concurrently from 20 parallel tasks
        Parallel.For(0, 20, _ =>
        {
            notification.MarkAsRead();
        });

        // Assert
        Assert.True(notification.IsRead);
        Assert.NotNull(notification.ReadAt);
    }

    [Theory]
    [InlineData(NotificationStatus.PENDING)]
    [InlineData(NotificationStatus.PROCESSING)]
    [InlineData(NotificationStatus.DELIVERED)]
    [InlineData(NotificationStatus.FAILED)]
    [InlineData(NotificationStatus.DEAD_LETTER)]
    public void MarkAsRead_AcrossAllNotificationStatuses_ShouldSucceedWithoutModifyingLifecycleStatus(NotificationStatus status)
    {
        // Arrange
        var notification = DurableNotification.Create(
            "TestEvent",
            $"KEY-{status}",
            Guid.NewGuid(),
            NotificationChannel.IN_APP,
            "TPL",
            "{}");

        // Transition to designated status
        if (status == NotificationStatus.PROCESSING)
        {
            notification.MarkProcessing();
        }
        else if (status == NotificationStatus.DELIVERED)
        {
            notification.MarkProcessing();
            notification.MarkDelivered();
        }
        else if (status == NotificationStatus.FAILED)
        {
            notification.MarkProcessing();
            notification.RecordFailure("Temporary error");
        }
        else if (status == NotificationStatus.DEAD_LETTER)
        {
            // MaxAttempts = 3 by default
            notification.MarkProcessing();
            notification.RecordFailure("Error 1");
            notification.MarkProcessing();
            notification.RecordFailure("Error 2");
            notification.MarkProcessing();
            notification.RecordFailure("Error 3");
        }

        Assert.Equal(status, notification.Status);

        // Act
        notification.MarkAsRead();

        // Assert
        Assert.True(notification.IsRead);
        Assert.NotNull(notification.ReadAt);
        Assert.Equal(status, notification.Status); // Lifecycle status must remain untouched
    }

    [Fact]
    public void OtherLifecycleMutations_ShouldNotOverwriteReadState()
    {
        // Arrange
        var notification = DurableNotification.Create(
            "Event", "KEY-LIFECYCLE-READ", Guid.NewGuid(), NotificationChannel.IN_APP, "TPL", "{}");

        notification.MarkAsRead();
        var readAt = notification.ReadAt;
        Assert.True(notification.IsRead);
        Assert.NotNull(readAt);

        // Act: mark processing
        notification.MarkProcessing();
        Assert.True(notification.IsRead);
        Assert.Equal(readAt, notification.ReadAt);

        // Act: mark delivered
        notification.MarkDelivered();
        Assert.True(notification.IsRead);
        Assert.Equal(readAt, notification.ReadAt);
    }

    [Theory]
    [InlineData(null, null, "", "")]
    [InlineData("", "", "", "")]
    [InlineData("   ", "   \t\n", "", "")]
    [InlineData("  Thanh toán thành công  ", "  Chi tiết giao dịch  ", "Thanh toán thành công", "Chi tiết giao dịch")]
    public void Create_WithVariousTitleAndMessageEdgeCases_ShouldNormalizeCorrectly(
        string? inputTitle,
        string? inputMessage,
        string expectedTitle,
        string expectedMessage)
    {
        // Arrange & Act
        var notification = DurableNotification.Create(
            originatingEvent: "PaymentSucceeded",
            idempotencyKey: "PAY-EDGE-" + Guid.NewGuid().ToString("N"),
            recipientId: Guid.NewGuid(),
            channel: NotificationChannel.IN_APP,
            templateCode: "PAYMENT_CONFIRMATION",
            payload: "{}",
            title: inputTitle!,
            message: inputMessage!);

        // Assert
        Assert.Equal(expectedTitle, notification.Title);
        Assert.Equal(expectedMessage, notification.Message);
        Assert.False(notification.IsRead);
        Assert.Null(notification.ReadAt);
    }

    [Fact]
    public void Create_WithExtremeLengthAndSpecialCharacters_ShouldHandleGracefully()
    {
        // Arrange
        var extremeTitle = new string('A', 500); // 500 chars
        var extremeMessage = new string('X', 50000); // 50KB message
        var unicodeTitle = "🔔 Thông báo đỗ xe: Ô A-08 đã mở khóa! 🚗💨";
        var sqlInjectionPayload = "'; DROP TABLE durable_notifications; --";

        // Act
        var notif1 = DurableNotification.Create(
            "Event1", "KEY-EXTREME-1", Guid.NewGuid(), NotificationChannel.IN_APP, "TPL", "{}",
            title: extremeTitle, message: extremeMessage);

        var notif2 = DurableNotification.Create(
            "Event2", "KEY-EXTREME-2", Guid.NewGuid(), NotificationChannel.IN_APP, "TPL", "{}",
            title: unicodeTitle, message: sqlInjectionPayload);

        // Assert
        Assert.Equal(extremeTitle, notif1.Title);
        Assert.Equal(extremeMessage, notif1.Message);
        Assert.Equal(unicodeTitle, notif2.Title);
        Assert.Equal(sqlInjectionPayload, notif2.Message);
    }

    [Fact]
    public void NotificationDto_FromEntity_WhenUnread_ShouldReflectUnreadCorrectly()
    {
        // Arrange
        var recipientId = Guid.NewGuid();
        var notification = DurableNotification.Create(
            "ReservationCreated",
            "RES-UNREAD-1",
            recipientId,
            NotificationChannel.SMS,
            "SMS_TPL",
            "{}",
            title: "SMS Title",
            message: "SMS Body");

        // Act
        var dto = NotificationDto.FromEntity(notification);

        // Assert
        Assert.False(dto.IsRead);
        Assert.Null(dto.ReadAt);
        Assert.Equal("SMS Title", dto.Title);
        Assert.Equal("SMS Body", dto.Message);
        Assert.Equal("SMS", dto.Channel);
    }
}
