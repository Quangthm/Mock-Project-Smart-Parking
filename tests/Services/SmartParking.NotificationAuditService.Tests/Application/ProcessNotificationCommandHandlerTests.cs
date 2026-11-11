using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.NotificationAuditService.Application.Usecase.Notifications.Commands.MarkAllNotificationsRead;
using SmartParking.NotificationAuditService.Application.Usecase.Notifications.Commands.MarkNotificationRead;
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
    private readonly FakeCentralAuditProjectionRepository _auditRepo = new();
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
            _auditRepo,
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
            _auditRepo,
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
        Assert.Single(_auditRepo.Projections);
    }

    [Fact]
    public async Task ProcessPayment_WhenRegisteredUser_ShouldCreateInAppNotificationWithIctGracePeriodAndCentralAuditProjection()
    {
        var handler = new ProcessPaymentNotificationCommandHandler(
            _notificationRepo,
            _auditRepo,
            _dispatcher,
            _uow,
            NullLogger<ProcessPaymentNotificationCommandHandler>.Instance);

        var eventId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var fixedTime = new DateTimeOffset(2026, 11, 5, 8, 30, 0, TimeSpan.Zero); // 08:30:00 UTC = 15:30:00 ICT

        var command = new ProcessPaymentNotificationCommand
        {
            EventId = eventId,
            OccurredAt = fixedTime,
            CorrelationId = "CORR-REG-123",
            PaymentOrderId = orderId,
            PaymentTransactionId = Guid.NewGuid(),
            PaymentMethod = "PAYOS",
            GatewayReference = "PAYOS-REF",
            TenantId = Guid.NewGuid(),
            SiteId = Guid.NewGuid(),
            ReferenceType = "PARKING_SESSION",
            ReferenceId = Guid.NewGuid(),
            PlateNumber = "51A-123.45",
            AccountId = accountId,
            Amount = 150000m,
            ParkingFee = 150000m,
            PenaltyFee = null,
            PenaltyReason = null,
            Currency = "VND"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.Success);
        Assert.False(result.IsDuplicate);
        Assert.Equal("DELIVERED", result.Status);

        // 1. Verify DurableNotification
        Assert.Single(_notificationRepo.Notifications);
        var notif = _notificationRepo.Notifications[0];
        Assert.Equal(accountId, notif.RecipientId);
        Assert.Equal(NotificationChannel.IN_APP, notif.Channel);
        Assert.Equal("PAYMENT_SUCCESS", notif.TemplateCode);
        Assert.Equal("Thanh toán thành công", notif.Title);
        // 08:30:00 UTC + 15 min = 08:45:00 UTC -> 15:45:00 ICT
        Assert.Contains("15:45:00", notif.Message);
        Assert.Contains("ân hạn 15 phút", notif.Message);
        Assert.Contains("51A-123.45", notif.Message);
        Assert.Contains("150,000 VND", notif.Message);

        // 2. Verify CentralAuditProjection
        Assert.Single(_auditRepo.Projections);
        var audit = _auditRepo.Projections[0];
        Assert.Equal(eventId, audit.EventId);
        Assert.Equal("PaymentService", audit.OriginatingService);
        Assert.Equal("PAYMENT_SUCCEEDED", audit.Action);
        Assert.Equal("DRIVER", audit.ActorRole);
        Assert.Equal(accountId, audit.ActorId);
        Assert.Equal(orderId, audit.ResourceId);
    }

    [Fact]
    public async Task ProcessPayment_WhenGuestUser_ShouldRecordCentralAuditProjectionOnlyAndZeroDurableNotification()
    {
        var handler = new ProcessPaymentNotificationCommandHandler(
            _notificationRepo,
            _auditRepo,
            _dispatcher,
            _uow,
            NullLogger<ProcessPaymentNotificationCommandHandler>.Instance);

        var eventId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var command = new ProcessPaymentNotificationCommand
        {
            EventId = eventId,
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = "CORR-GUEST-456",
            PaymentOrderId = orderId,
            PaymentTransactionId = Guid.NewGuid(),
            PaymentMethod = "CASH",
            GatewayReference = null,
            TenantId = Guid.NewGuid(),
            SiteId = Guid.NewGuid(),
            ReferenceType = "PARKING_SESSION",
            ReferenceId = Guid.NewGuid(),
            PlateNumber = "59-B1 678.90",
            AccountId = null, // Guest
            Amount = 30000m,
            ParkingFee = 30000m,
            Currency = "VND"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.Success);
        Assert.False(result.IsDuplicate);

        // 1. Zero DurableNotification created
        Assert.Empty(_notificationRepo.Notifications);

        // 2. CentralAuditProjection recorded with GUEST
        Assert.Single(_auditRepo.Projections);
        var audit = _auditRepo.Projections[0];
        Assert.Equal(eventId, audit.EventId);
        Assert.Equal("PaymentService", audit.OriginatingService);
        Assert.Equal("PAYMENT_SUCCEEDED", audit.Action);
        Assert.Equal("GUEST", audit.ActorRole);
        Assert.Equal(Guid.Empty, audit.ActorId);
        Assert.Equal(orderId, audit.ResourceId);
    }

    [Fact]
    public async Task MarkNotificationRead_WhenValidAuthorizedUser_ShouldUpdateIsReadAndReadAtAndReturnDto()
    {
        var recipientId = Guid.NewGuid();
        var notif = DurableNotification.Create(
            "PaymentSucceededV1",
            "KEY-READ-1",
            recipientId,
            NotificationChannel.IN_APP,
            "PAYMENT_SUCCESS",
            "{}",
            title: "Thanh toán thành công",
            message: "Test message");

        await _notificationRepo.AddAsync(notif);
        Assert.False(notif.IsRead);
        Assert.Null(notif.ReadAt);

        var handler = new MarkNotificationReadCommandHandler(
            _notificationRepo,
            _uow,
            NullLogger<MarkNotificationReadCommandHandler>.Instance);

        var command = new MarkNotificationReadCommand
        {
            Id = notif.Id,
            RequestingUserId = recipientId
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.IsRead);
        Assert.NotNull(result.ReadAt);
        Assert.Equal(notif.Id, result.Id);
        Assert.Equal(1, _uow.SaveCount);

        var updated = await _notificationRepo.GetByIdAsync(notif.Id);
        Assert.NotNull(updated);
        Assert.True(updated.IsRead);
        Assert.NotNull(updated.ReadAt);
    }

    [Fact]
    public async Task MarkNotificationRead_WhenCrossUserUnauthorized_ShouldThrowNotificationForbiddenException()
    {
        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();

        var notif = DurableNotification.Create(
            "PaymentSucceededV1",
            "KEY-READ-2",
            ownerId,
            NotificationChannel.IN_APP,
            "PAYMENT_SUCCESS",
            "{}");

        await _notificationRepo.AddAsync(notif);

        var handler = new MarkNotificationReadCommandHandler(
            _notificationRepo,
            _uow,
            NullLogger<MarkNotificationReadCommandHandler>.Instance);

        var command = new MarkNotificationReadCommand
        {
            Id = notif.Id,
            RequestingUserId = attackerId
        };

        await Assert.ThrowsAsync<NotificationForbiddenException>(() =>
            handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task MarkNotificationRead_WhenNotFound_ShouldReturnNull()
    {
        var handler = new MarkNotificationReadCommandHandler(
            _notificationRepo,
            _uow,
            NullLogger<MarkNotificationReadCommandHandler>.Instance);

        var command = new MarkNotificationReadCommand
        {
            Id = Guid.NewGuid(),
            RequestingUserId = Guid.NewGuid()
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task MarkAllNotificationsRead_ShouldMarkAllForRecipientAndReturnCount()
    {
        var recipientA = Guid.NewGuid();
        var recipientB = Guid.NewGuid();

        var n1 = DurableNotification.Create("Pay", "K1", recipientA, NotificationChannel.IN_APP, "T1", "{}");
        var n2 = DurableNotification.Create("Pay", "K2", recipientA, NotificationChannel.IN_APP, "T1", "{}");
        var n3 = DurableNotification.Create("Pay", "K3", recipientA, NotificationChannel.IN_APP, "T1", "{}");
        n3.MarkAsRead(); // already read
        var n4 = DurableNotification.Create("Pay", "K4", recipientB, NotificationChannel.IN_APP, "T1", "{}"); // other recipient

        await _notificationRepo.AddAsync(n1);
        await _notificationRepo.AddAsync(n2);
        await _notificationRepo.AddAsync(n3);
        await _notificationRepo.AddAsync(n4);

        var handler = new MarkAllNotificationsReadCommandHandler(
            _notificationRepo,
            NullLogger<MarkAllNotificationsReadCommandHandler>.Instance);

        var command = new MarkAllNotificationsReadCommand(recipientA);

        var count = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(2, count);

        // Verify state
        var a1 = await _notificationRepo.GetByIdAsync(n1.Id);
        var a2 = await _notificationRepo.GetByIdAsync(n2.Id);
        var b4 = await _notificationRepo.GetByIdAsync(n4.Id);

        Assert.True(a1!.IsRead);
        Assert.True(a2!.IsRead);
        Assert.False(b4!.IsRead);
    }

    [Fact]
    public void ProcessPaymentCommandValidator_GuestFlow_ShouldPassValidation()
    {
        var validator = new ProcessPaymentNotificationCommandValidator();
        var command = new ProcessPaymentNotificationCommand
        {
            EventId = Guid.NewGuid(),
            PaymentOrderId = Guid.NewGuid(),
            AccountId = null, // Guest
            Amount = 25000m,
            Currency = "VND",
            PaymentMethod = "CASH"
        };

        var result = validator.Validate(command);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void MarkNotificationReadValidator_EmptyId_ShouldFailValidation()
    {
        var validator = new MarkNotificationReadCommandValidator();
        var command = new MarkNotificationReadCommand { Id = Guid.Empty };

        var result = validator.Validate(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Id");
    }

    [Fact]
    public void MarkAllNotificationsReadValidator_EmptyRecipientId_ShouldFailValidation()
    {
        var validator = new MarkAllNotificationsReadCommandValidator();
        var command = new MarkAllNotificationsReadCommand(Guid.Empty);

        var result = validator.Validate(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "RecipientId");
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
