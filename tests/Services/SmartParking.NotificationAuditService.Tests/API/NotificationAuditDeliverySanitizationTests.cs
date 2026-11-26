using Microsoft.Extensions.Logging;
using SmartParking.NotificationAuditService.Application.Usecase.Notifications.Commands.ProcessPaymentNotification;
using SmartParking.NotificationAuditService.Domain.Entities;
using SmartParking.NotificationAuditService.Domain.Enums;
using SmartParking.NotificationAuditService.Infrastructure.Adapters.Delivery;
using SmartParking.NotificationAuditService.Tests.API.Probes;
using SmartParking.NotificationAuditService.Tests.TestDoubles;

namespace SmartParking.NotificationAuditService.Tests.API;

public sealed class NotificationAuditDeliverySanitizationTests
{
    [Fact]
    public async Task DispatcherException_IsSanitizedAndDoesNotThrowOrLeakCredentials()
    {
        var notificationRepo = new FakeNotificationRepository();
        var templateRepo = new FakeNotificationTemplateRepository();
        var unitOfWork = new FakeUnitOfWork();
        var logs = new NotificationErrorCapturedLogs();
        var logger = logs.CreateLogger(typeof(NotificationDispatcher).FullName!);
        var typedLogger = LoggerFactory
            .Create(builder => builder.AddProvider(logs))
            .CreateLogger<NotificationDispatcher>();

        var adapter = new ThrowingNotificationChannelAdapter(
            NotificationChannel.IN_APP,
            _ => new InvalidOperationException("secret-smtp-password=12345 endpoint=https://internal-provider.local"));

        var dispatcher = new NotificationDispatcher(
            [adapter],
            templateRepo,
            notificationRepo,
            unitOfWork,
            typedLogger);

        var notification = DurableNotification.Create(
            originatingEvent: "PaymentSucceededV1",
            idempotencyKey: "PAY_TEST_SANITIZE_1",
            recipientId: Guid.NewGuid(),
            channel: NotificationChannel.IN_APP,
            templateCode: "PAYMENT_SUCCESS",
            payload: "{}");
        await notificationRepo.AddAsync(notification);

        var result = await dispatcher.DispatchAsync(notification);

        Assert.False(result.Success);
        Assert.Equal("FAILED", result.Status);
        Assert.DoesNotContain("secret", result.ErrorMessage ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", result.ErrorMessage ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", notification.LastError ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "secret",
            string.Join(" ", logs.Entries.Select(entry => entry.Message)),
            StringComparison.OrdinalIgnoreCase);
        var errorEntry = Assert.Single(logs.Entries, entry => entry.Level == LogLevel.Error);
        Assert.Null(errorEntry.Exception);
    }

    [Fact]
    public async Task DispatcherCallerCancellation_PropagatesOperationCanceledException()
    {
        var notificationRepo = new FakeNotificationRepository();
        var templateRepo = new FakeNotificationTemplateRepository();
        var unitOfWork = new FakeUnitOfWork();
        var logger = LoggerFactory.Create(_ => { }).CreateLogger<NotificationDispatcher>();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var adapter = new ThrowingNotificationChannelAdapter(
            NotificationChannel.IN_APP,
            ct => new OperationCanceledException(ct));

        var dispatcher = new NotificationDispatcher(
            [adapter],
            templateRepo,
            notificationRepo,
            unitOfWork,
            logger);

        var notification = DurableNotification.Create(
            originatingEvent: "PaymentSucceededV1",
            idempotencyKey: "PAY_TEST_CANCEL_1",
            recipientId: Guid.NewGuid(),
            channel: NotificationChannel.IN_APP,
            templateCode: "PAYMENT_SUCCESS",
            payload: "{}");
        await notificationRepo.AddAsync(notification);

        await Assert.ThrowsAsync<OperationCanceledException>(() => dispatcher.DispatchAsync(notification, cts.Token));
    }

    [Fact]
    public async Task PaymentNotification_WhenDispatcherThrows_RecordsSanitizedFailureWithoutRollingBackAudit()
    {
        var notificationRepo = new FakeNotificationRepository();
        var auditRepo = new FakeCentralAuditProjectionRepository();
        var unitOfWork = new FakeUnitOfWork();
        var logs = new NotificationErrorCapturedLogs();
        var logger = LoggerFactory
            .Create(builder => builder.AddProvider(logs))
            .CreateLogger<ProcessPaymentNotificationCommandHandler>();

        var throwingDispatcher = new ThrowingNotificationDispatcher(
            () => new HttpRequestException("secret-provider-api-key=abcxyz"));

        var handler = new ProcessPaymentNotificationCommandHandler(
            notificationRepo,
            auditRepo,
            throwingDispatcher,
            unitOfWork,
            logger);

        var command = new ProcessPaymentNotificationCommand
        {
            EventId = Guid.NewGuid(),
            PaymentOrderId = Guid.NewGuid(),
            PaymentTransactionId = Guid.NewGuid(),
            PaymentMethod = "PAYOS",
            AccountId = Guid.NewGuid(),
            Amount = 50000m,
            Currency = "VND",
            OccurredAt = DateTimeOffset.UtcNow
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Single(auditRepo.Projections);
        Assert.Single(notificationRepo.Notifications);
        Assert.DoesNotContain("secret", result.ErrorMessage ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "secret",
            string.Join(" ", logs.Entries.Select(entry => entry.Message)),
            StringComparison.OrdinalIgnoreCase);
    }
}
