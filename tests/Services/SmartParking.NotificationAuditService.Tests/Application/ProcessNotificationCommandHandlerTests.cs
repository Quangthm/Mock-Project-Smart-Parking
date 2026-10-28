using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.NotificationAuditService.Application.Usecase.Notifications.Commands.ProcessPaymentNotification;
using SmartParking.NotificationAuditService.Application.Usecase.Notifications.Commands.ProcessReservationNotification;
using SmartParking.NotificationAuditService.Application.Usecase.Notifications.Commands.RetryNotificationDelivery;
using SmartParking.NotificationAuditService.Application.Usecase.Notifications.Queries.GetDriverNotifications;
using SmartParking.NotificationAuditService.Application.Usecase.Notifications.Queries.GetNotificationById;
using SmartParking.NotificationAuditService.Domain.Entities;
using SmartParking.NotificationAuditService.Domain.Enums;
using SmartParking.NotificationAuditService.Domain.Exceptions;
using SmartParking.NotificationAuditService.Tests.TestDoubles;

namespace SmartParking.NotificationAuditService.Tests.Application;

public class ProcessNotificationCommandHandlerTests
{
    private readonly FakeNotificationRepository _notificationRepo = new();
    private readonly FakeNotificationDispatcher _dispatcher = new();
    private readonly FakeUnitOfWork _uow = new();

    [Fact]
    public async Task ProcessReservation_WhenNewEvent_ShouldCreateAndDeliverDurableNotification()
    {
        var handler = new ProcessReservationNotificationCommandHandler(
            _notificationRepo,
            _dispatcher,
            _uow,
            NullLogger<ProcessReservationNotificationCommandHandler>.Instance);

        var eventId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var command = new ProcessReservationNotificationCommand
        {
            EventId = eventId,
            ReservationId = Guid.NewGuid(),
            DriverId = driverId,
            SiteId = Guid.NewGuid(),
            SiteName = "Lot A",
            SlotCode = "A-01",
            StartTime = DateTimeOffset.UtcNow,
            EndTime = DateTimeOffset.UtcNow.AddHours(2),
            OccurredAt = DateTimeOffset.UtcNow
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.Success);
        Assert.False(result.IsDuplicate);
        Assert.Equal("DELIVERED", result.Status);
        Assert.Single(_notificationRepo.Notifications);

        var saved = _notificationRepo.Notifications[0];
        Assert.Equal(driverId, saved.RecipientId);
        Assert.Equal(NotificationStatus.DELIVERED, saved.Status);
        Assert.Equal($"RES_{eventId}_{driverId}", saved.IdempotencyKey);
    }

    [Fact]
    public async Task ProcessReservation_WhenDuplicateEventReplayed_ShouldAcknowledgeWithoutCreatingDuplicate()
    {
        var handler = new ProcessReservationNotificationCommandHandler(
            _notificationRepo,
            _dispatcher,
            _uow,
            NullLogger<ProcessReservationNotificationCommandHandler>.Instance);

        var eventId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var command = new ProcessReservationNotificationCommand
        {
            EventId = eventId,
            ReservationId = Guid.NewGuid(),
            DriverId = driverId,
            SiteId = Guid.NewGuid(),
            SlotCode = "A-01",
            StartTime = DateTimeOffset.UtcNow,
            EndTime = DateTimeOffset.UtcNow.AddHours(2),
            OccurredAt = DateTimeOffset.UtcNow
        };

        // First call
        var firstResult = await handler.Handle(command, CancellationToken.None);
        Assert.True(firstResult.Success);
        Assert.False(firstResult.IsDuplicate);

        // Replay call with exact same EventId and DriverId
        var replayResult = await handler.Handle(command, CancellationToken.None);
        Assert.True(replayResult.Success);
        Assert.True(replayResult.IsDuplicate);
        Assert.Equal("DUPLICATE_ACKNOWLEDGED", replayResult.Status);

        // Assert strictly one entity in repository
        Assert.Single(_notificationRepo.Notifications);
    }

    [Fact]
    public async Task ProcessReservation_WhenDeliveryFails_ShouldPersistFailureIndependentlyWithoutThrowing()
    {
        _dispatcher.ShouldFail = true;
        _dispatcher.FailureMessage = "Email capture temporary error";

        var handler = new ProcessReservationNotificationCommandHandler(
            _notificationRepo,
            _dispatcher,
            _uow,
            NullLogger<ProcessReservationNotificationCommandHandler>.Instance);

        var command = new ProcessReservationNotificationCommand
        {
            EventId = Guid.NewGuid(),
            ReservationId = Guid.NewGuid(),
            DriverId = Guid.NewGuid(),
            SiteId = Guid.NewGuid(),
            SlotCode = "B-05",
            StartTime = DateTimeOffset.UtcNow,
            EndTime = DateTimeOffset.UtcNow.AddHours(1),
            OccurredAt = DateTimeOffset.UtcNow
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("FAILED", result.Status);
        Assert.Contains("Email capture temporary error", result.ErrorMessage);

        // Entity is preserved in FAILED status with LastError recorded
        Assert.Single(_notificationRepo.Notifications);
        var saved = _notificationRepo.Notifications[0];
        Assert.Equal(NotificationStatus.FAILED, saved.Status);
        Assert.Equal("Email capture temporary error", saved.LastError);
    }

    [Fact]
    public async Task ProcessPayment_WhenNewEvent_ShouldCreateAndDeliverDurableNotification()
    {
        var handler = new ProcessPaymentNotificationCommandHandler(
            _notificationRepo,
            _dispatcher,
            _uow,
            NullLogger<ProcessPaymentNotificationCommandHandler>.Instance);

        var eventId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var command = new ProcessPaymentNotificationCommand
        {
            EventId = eventId,
            PaymentOrderId = Guid.NewGuid(),
            DriverId = driverId,
            Amount = 50000m,
            Currency = "VND",
            PaymentMethod = "PAYOS",
            TransactionId = "TXN_123456",
            OccurredAt = DateTimeOffset.UtcNow
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.Success);
        Assert.False(result.IsDuplicate);
        Assert.Equal("DELIVERED", result.Status);
        Assert.Single(_notificationRepo.Notifications);

        var saved = _notificationRepo.Notifications[0];
        Assert.Equal(driverId, saved.RecipientId);
        Assert.Equal(NotificationStatus.DELIVERED, saved.Status);
        Assert.Equal($"PAY_{eventId}_{driverId}", saved.IdempotencyKey);
    }

    [Fact]
    public async Task ProcessPayment_WhenDuplicateReplayed_ShouldAcknowledgeWithoutCreatingDuplicate()
    {
        var handler = new ProcessPaymentNotificationCommandHandler(
            _notificationRepo,
            _dispatcher,
            _uow,
            NullLogger<ProcessPaymentNotificationCommandHandler>.Instance);

        var eventId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var command = new ProcessPaymentNotificationCommand
        {
            EventId = eventId,
            PaymentOrderId = Guid.NewGuid(),
            DriverId = driverId,
            Amount = 50000m,
            Currency = "VND",
            PaymentMethod = "PAYOS",
            TransactionId = "TXN_123456",
            OccurredAt = DateTimeOffset.UtcNow
        };

        await handler.Handle(command, CancellationToken.None);
        var replayResult = await handler.Handle(command, CancellationToken.None);

        Assert.True(replayResult.Success);
        Assert.True(replayResult.IsDuplicate);
        Assert.Single(_notificationRepo.Notifications);
    }

    [Fact]
    public async Task RetryDelivery_WhenAuthorizedRecipientAndFailed_ShouldSuccessfullyRetry()
    {
        var recipientId = Guid.NewGuid();
        var notification = DurableNotification.Create(
            "ReservationConfirmed",
            "KEY_01",
            recipientId,
            NotificationChannel.IN_APP,
            "RESERVATION_CONFIRMED",
            "{}");

        notification.MarkProcessing();
        notification.RecordFailure("Network glitch");
        await _notificationRepo.AddAsync(notification);

        var handler = new RetryNotificationDeliveryCommandHandler(
            _notificationRepo,
            _dispatcher,
            _uow,
            NullLogger<RetryNotificationDeliveryCommandHandler>.Instance);

        var command = new RetryNotificationDeliveryCommand
        {
            NotificationId = notification.Id,
            RequestingUserId = recipientId,
            RequestingUserRole = "Driver"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("DELIVERED", result.Status);
        Assert.Equal(NotificationStatus.DELIVERED, notification.Status);
    }

    [Fact]
    public async Task RetryDelivery_WhenAlreadyDelivered_ShouldBeIdempotentAndNotReDispatch()
    {
        var recipientId = Guid.NewGuid();
        var notification = DurableNotification.Create(
            "ReservationConfirmed",
            "KEY_02",
            recipientId,
            NotificationChannel.IN_APP,
            "RESERVATION_CONFIRMED",
            "{}");

        notification.MarkProcessing();
        notification.MarkDelivered();
        await _notificationRepo.AddAsync(notification);

        var initialDispatchCalls = _dispatcher.DispatchCalls;

        var handler = new RetryNotificationDeliveryCommandHandler(
            _notificationRepo,
            _dispatcher,
            _uow,
            NullLogger<RetryNotificationDeliveryCommandHandler>.Instance);

        var command = new RetryNotificationDeliveryCommand
        {
            NotificationId = notification.Id,
            RequestingUserId = recipientId,
            RequestingUserRole = "Driver"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("DELIVERED", result.Status);
        Assert.Equal(initialDispatchCalls, _dispatcher.DispatchCalls);
    }

    [Fact]
    public async Task RetryDelivery_WhenCrossUserUnauthorized_ShouldThrowNotificationForbiddenException()
    {
        var recipientId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();

        var notification = DurableNotification.Create(
            "PaymentVerified",
            "KEY_03",
            recipientId,
            NotificationChannel.IN_APP,
            "PAYMENT_SUCCESS",
            "{}");
        await _notificationRepo.AddAsync(notification);

        var handler = new RetryNotificationDeliveryCommandHandler(
            _notificationRepo,
            _dispatcher,
            _uow,
            NullLogger<RetryNotificationDeliveryCommandHandler>.Instance);

        var command = new RetryNotificationDeliveryCommand
        {
            NotificationId = notification.Id,
            RequestingUserId = attackerId,
            RequestingUserRole = "Driver"
        };

        await Assert.ThrowsAsync<NotificationForbiddenException>(() =>
            handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task GetNotificationById_WhenCrossUserUnauthorized_ShouldThrowNotificationForbiddenException()
    {
        var recipientId = Guid.NewGuid();
        var unauthorizedUser = Guid.NewGuid();

        var notification = DurableNotification.Create(
            "ReservationConfirmed",
            "KEY_04",
            recipientId,
            NotificationChannel.IN_APP,
            "RESERVATION_CONFIRMED",
            "{}");
        await _notificationRepo.AddAsync(notification);

        var handler = new GetNotificationByIdQueryHandler(
            _notificationRepo,
            NullLogger<GetNotificationByIdQueryHandler>.Instance);

        var query = new GetNotificationByIdQuery
        {
            NotificationId = notification.Id,
            RequestingUserId = unauthorizedUser,
            RequestingUserRole = "Driver"
        };

        await Assert.ThrowsAsync<NotificationForbiddenException>(() =>
            handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async Task GetDriverNotifications_ShouldReturnOnlyRecipientNotifications()
    {
        var driver1 = Guid.NewGuid();
        var driver2 = Guid.NewGuid();

        var n1 = DurableNotification.Create("Res", "K1", driver1, NotificationChannel.IN_APP, "T1", "{}");
        var n2 = DurableNotification.Create("Res", "K2", driver1, NotificationChannel.IN_APP, "T1", "{}");
        var n3 = DurableNotification.Create("Res", "K3", driver2, NotificationChannel.IN_APP, "T1", "{}");

        await _notificationRepo.AddAsync(n1);
        await _notificationRepo.AddAsync(n2);
        await _notificationRepo.AddAsync(n3);

        var handler = new GetDriverNotificationsQueryHandler(
            _notificationRepo,
            NullLogger<GetDriverNotificationsQueryHandler>.Instance);

        var query = new GetDriverNotificationsQuery
        {
            RecipientId = driver1,
            Limit = 10
        };

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal(driver1, r.RecipientId));
    }
}
