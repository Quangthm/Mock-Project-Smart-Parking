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
}
