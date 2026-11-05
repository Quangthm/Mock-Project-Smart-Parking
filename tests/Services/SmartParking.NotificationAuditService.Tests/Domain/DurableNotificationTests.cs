using SmartParking.NotificationAuditService.Application.DTOs.Notifications;
using SmartParking.NotificationAuditService.Domain.Entities;
using SmartParking.NotificationAuditService.Domain.Enums;
using SmartParking.NotificationAuditService.Domain.Exceptions;

namespace SmartParking.NotificationAuditService.Tests.Domain;

public class DurableNotificationTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldInitializeCorrectly()
    {
        var recipientId = Guid.NewGuid();
        var notification = DurableNotification.Create(
            originatingEvent: "PaymentVerified",
            idempotencyKey: "PAY-999",
            recipientId: recipientId,
            channel: NotificationChannel.EMAIL,
            templateCode: "PAYMENT_RECEIPT",
            payload: "{\"amount\": 50000}",
            maxAttempts: 4);

        Assert.Equal("PaymentVerified", notification.OriginatingEvent);
        Assert.Equal("PAY-999", notification.IdempotencyKey);
        Assert.Equal(recipientId, notification.RecipientId);
        Assert.Equal(NotificationChannel.EMAIL, notification.Channel);
        Assert.Equal("PAYMENT_RECEIPT", notification.TemplateCode);
        Assert.Equal(NotificationStatus.PENDING, notification.Status);
        Assert.Equal(0, notification.Attempts);
        Assert.Equal(4, notification.MaxAttempts);
        Assert.Null(notification.DeliveredAt);
    }

    [Theory]
    [InlineData("", "KEY-1", "TEMPLATE")]
    [InlineData("EVENT", "", "TEMPLATE")]
    [InlineData("EVENT", "KEY-1", "")]
    public void Create_WithMissingRequiredFields_ShouldThrowDomainException(
        string originatingEvent,
        string idempotencyKey,
        string templateCode)
    {
        Assert.Throws<NotificationDomainException>(() =>
            DurableNotification.Create(
                originatingEvent,
                idempotencyKey,
                Guid.NewGuid(),
                NotificationChannel.IN_APP,
                templateCode,
                "{}"));
    }

    [Fact]
    public void StateTransitions_MarkProcessingAndDelivered_ShouldUpdateStatus()
    {
        var notification = DurableNotification.Create(
            "Event", "Key-1", Guid.NewGuid(), NotificationChannel.IN_APP, "TPL", "{}");

        notification.MarkProcessing();
        Assert.Equal(NotificationStatus.PROCESSING, notification.Status);
        Assert.Equal(1, notification.Attempts);

        notification.MarkDelivered();
        Assert.Equal(NotificationStatus.DELIVERED, notification.Status);
        Assert.NotNull(notification.DeliveredAt);
        Assert.Null(notification.LastError);
    }

    [Fact]
    public void RecordFailure_WhenAttemptsReachMax_ShouldTransitionToDeadLetter()
    {
        var notification = DurableNotification.Create(
            "Event", "Key-2", Guid.NewGuid(), NotificationChannel.IN_APP, "TPL", "{}", maxAttempts: 2);

        notification.MarkProcessing(); // Attempt 1
        notification.RecordFailure("Network timeout");
        Assert.Equal(NotificationStatus.FAILED, notification.Status);

        notification.MarkProcessing(); // Attempt 2
        notification.RecordFailure("Network timeout again");
        Assert.Equal(NotificationStatus.DEAD_LETTER, notification.Status);
        Assert.Equal("Network timeout again", notification.LastError);
    }

    [Fact]
    public void MarkProcessing_WhenAlreadyDelivered_ShouldThrowDomainException()
    {
        var notification = DurableNotification.Create(
            "Event", "Key-3", Guid.NewGuid(), NotificationChannel.IN_APP, "TPL", "{}");

        notification.MarkDelivered();

        Assert.Throws<NotificationDomainException>(() => notification.MarkProcessing());
    }

    [Fact]
    public void Create_WithTitleAndMessage_ShouldInitializeCorrectly()
    {
        var recipientId = Guid.NewGuid();
        var notification = DurableNotification.Create(
            originatingEvent: "PaymentSucceeded",
            idempotencyKey: "PAY-123456",
            recipientId: recipientId,
            channel: NotificationChannel.IN_APP,
            templateCode: "PAYMENT_CONFIRMATION",
            payload: "{\"amount\": 50000}",
            maxAttempts: 3,
            scheduledAt: null,
            title: "Thanh toán thành công",
            message: "Bạn đã thanh toán 50.000 VND thành công.");

        Assert.Equal("Thanh toán thành công", notification.Title);
        Assert.Equal("Bạn đã thanh toán 50.000 VND thành công.", notification.Message);
        Assert.False(notification.IsRead);
        Assert.Null(notification.ReadAt);
    }

    [Fact]
    public void MarkAsRead_WhenUnread_ShouldSetIsReadAndTimestamp()
    {
        var notification = DurableNotification.Create(
            "Event", "Key-Read-1", Guid.NewGuid(), NotificationChannel.IN_APP, "TPL", "{}",
            title: "Test", message: "Body");

        Assert.False(notification.IsRead);
        Assert.Null(notification.ReadAt);

        var before = DateTimeOffset.UtcNow;
        notification.MarkAsRead();
        var after = DateTimeOffset.UtcNow;

        Assert.True(notification.IsRead);
        Assert.NotNull(notification.ReadAt);
        Assert.InRange(notification.ReadAt.Value, before, after);
        Assert.InRange(notification.UpdatedAt, before, after);
    }

    [Fact]
    public void MarkAsRead_WhenAlreadyRead_ShouldBeIdempotentAndPreserveReadAt()
    {
        var notification = DurableNotification.Create(
            "Event", "Key-Read-2", Guid.NewGuid(), NotificationChannel.IN_APP, "TPL", "{}");

        notification.MarkAsRead();
        var initialReadAt = notification.ReadAt;
        var initialUpdatedAt = notification.UpdatedAt;

        // Second call
        notification.MarkAsRead();

        Assert.True(notification.IsRead);
        Assert.Equal(initialReadAt, notification.ReadAt);
        Assert.Equal(initialUpdatedAt, notification.UpdatedAt);
    }

    [Fact]
    public void NotificationDto_FromEntity_ShouldMapAllFieldsIncludingTitleMessageIsReadAndReadAt()
    {
        var recipientId = Guid.NewGuid();
        var notification = DurableNotification.Create(
            originatingEvent: "PaymentSucceeded",
            idempotencyKey: "PAY-DTO-1",
            recipientId: recipientId,
            channel: NotificationChannel.IN_APP,
            templateCode: "PAYMENT_CONFIRMATION",
            payload: "{\"status\": \"PAID\"}",
            title: "Thanh toán dịch vụ",
            message: "Giao dịch hoàn tất.");

        notification.MarkAsRead();

        var dto = NotificationDto.FromEntity(notification);

        Assert.Equal(notification.Id, dto.Id);
        Assert.Equal("PaymentSucceeded", dto.OriginatingEvent);
        Assert.Equal(recipientId, dto.RecipientId);
        Assert.Equal("IN_APP", dto.Channel);
        Assert.Equal("PAYMENT_CONFIRMATION", dto.TemplateCode);
        Assert.Equal("Thanh toán dịch vụ", dto.Title);
        Assert.Equal("Giao dịch hoàn tất.", dto.Message);
        Assert.True(dto.IsRead);
        Assert.Equal(notification.ReadAt, dto.ReadAt);
    }
}
