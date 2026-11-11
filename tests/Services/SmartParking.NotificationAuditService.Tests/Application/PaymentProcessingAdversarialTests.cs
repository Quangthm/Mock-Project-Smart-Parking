using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.NotificationAuditService.Application.DTOs.Notifications;
using SmartParking.NotificationAuditService.Application.Usecase.Notifications.Commands.ProcessPaymentNotification;
using SmartParking.NotificationAuditService.Domain.Entities;
using SmartParking.NotificationAuditService.Domain.Enums;
using SmartParking.NotificationAuditService.Domain.Exceptions;
using SmartParking.NotificationAuditService.Tests.TestDoubles;

namespace SmartParking.NotificationAuditService.Tests.Application;

public class PaymentProcessingAdversarialTests
{
    private readonly FakeNotificationRepository _notificationRepo = new();
    private readonly FakeCentralAuditProjectionRepository _auditRepo = new();
    private readonly FakeNotificationDispatcher _dispatcher = new();
    private readonly FakeUnitOfWork _uow = new();

    private ProcessPaymentNotificationCommandHandler CreateHandler() =>
        new(
            _notificationRepo,
            _auditRepo,
            _dispatcher,
            _uow,
            NullLogger<ProcessPaymentNotificationCommandHandler>.Instance);

    #region 1. Guest Payment Processing Probes

    [Fact]
    public async Task GuestPayment_WithNullAccountId_MustProduceZeroDurableNotificationAndAuditWithGuestActor()
    {
        var handler = CreateHandler();
        var eventId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var txnId = Guid.NewGuid();

        var command = new ProcessPaymentNotificationCommand
        {
            EventId = eventId,
            PaymentOrderId = orderId,
            PaymentTransactionId = txnId,
            PaymentMethod = "CASH",
            AccountId = null, // Guest
            Amount = 45000m,
            ParkingFee = 45000m,
            Currency = "VND",
            PlateNumber = "29-A1 999.88",
            OccurredAt = DateTimeOffset.UtcNow
        };

        var result = await handler.Handle(command, CancellationToken.None);

        // Result verification
        Assert.True(result.Success);
        Assert.False(result.IsDuplicate);
        Assert.Equal("DELIVERED", result.Status);

        // Verification: ZERO DurableNotification
        Assert.Empty(_notificationRepo.Notifications);

        // Verification: Exactly 1 CentralAuditProjection with actor GUEST and ActorId = Guid.Empty
        Assert.Single(_auditRepo.Projections);
        var projection = _auditRepo.Projections[0];
        Assert.Equal("PaymentService", projection.OriginatingService);
        Assert.Equal(eventId, projection.EventId);
        Assert.Equal("GUEST", projection.ActorRole);
        Assert.Equal(Guid.Empty, projection.ActorId);
        Assert.Equal("PAYMENT_SUCCEEDED", projection.Action);
        Assert.Equal("PAYMENT_ORDER", projection.ResourceType);
        Assert.Equal(orderId, projection.ResourceId);

        // Verification: JSON payload integrity
        var detailsDoc = JsonDocument.Parse(projection.Details);
        Assert.Equal(orderId.ToString(), detailsDoc.RootElement.GetProperty("paymentOrderId").GetString());
        Assert.Equal(45000m, detailsDoc.RootElement.GetProperty("amount").GetDecimal());
        Assert.Equal("CASH", detailsDoc.RootElement.GetProperty("paymentMethod").GetString());

        // Verification: UoW committed
        Assert.Equal(1, _uow.SaveCount);
    }

    [Fact]
    public async Task GuestPayment_WithGuidEmptyAccountId_MustProduceZeroDurableNotificationAndAuditWithGuestActor()
    {
        var handler = CreateHandler();
        var eventId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var command = new ProcessPaymentNotificationCommand
        {
            EventId = eventId,
            PaymentOrderId = orderId,
            PaymentTransactionId = Guid.NewGuid(),
            PaymentMethod = "PAYOS",
            AccountId = Guid.Empty, // Explicitly empty GUID
            Amount = 30000m,
            Currency = "VND",
            OccurredAt = DateTimeOffset.UtcNow
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.Success);
        Assert.False(result.IsDuplicate);
        Assert.Empty(_notificationRepo.Notifications);

        Assert.Single(_auditRepo.Projections);
        var projection = _auditRepo.Projections[0];
        Assert.Equal("GUEST", projection.ActorRole);
        Assert.Equal(Guid.Empty, projection.ActorId);
    }

    [Fact]
    public async Task GuestPayment_WithMissingOptionalFields_ShouldNotThrowAndSerializeGracefully()
    {
        var handler = CreateHandler();
        var eventId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var command = new ProcessPaymentNotificationCommand
        {
            EventId = eventId,
            PaymentOrderId = orderId,
            PaymentTransactionId = Guid.NewGuid(),
            PaymentMethod = "CASH",
            AccountId = null,
            PlateNumber = null,
            GatewayReference = null,
            PenaltyFee = null,
            PenaltyReason = null,
            Amount = 10000m,
            Currency = "VND"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(_notificationRepo.Notifications);
        Assert.Single(_auditRepo.Projections);

        var detailsDoc = JsonDocument.Parse(_auditRepo.Projections[0].Details);
        Assert.Equal(JsonValueKind.Null, detailsDoc.RootElement.GetProperty("plateNumber").ValueKind);
        Assert.Equal(JsonValueKind.Null, detailsDoc.RootElement.GetProperty("gatewayReference").ValueKind);
    }

    [Fact]
    public void CentralAuditProjection_DomainInvariant_ActorIdEmptyAllowedOnlyForGuest()
    {
        // 1. GUEST with Guid.Empty actorId -> Allowed
        var guestProj = CentralAuditProjection.Create(
            originatingService: "PaymentService",
            eventId: Guid.NewGuid(),
            actorId: Guid.Empty,
            actorRole: "GUEST",
            action: "PAYMENT_SUCCEEDED",
            resourceType: "PAYMENT_ORDER",
            resourceId: Guid.NewGuid(),
            details: "{}",
            occurredAt: DateTimeOffset.UtcNow);

        Assert.NotNull(guestProj);
        Assert.Equal(Guid.Empty, guestProj.ActorId);
        Assert.Equal("GUEST", guestProj.ActorRole);

        // Case-insensitivity check: "guest"
        var guestLowerProj = CentralAuditProjection.Create(
            originatingService: "PaymentService",
            eventId: Guid.NewGuid(),
            actorId: Guid.Empty,
            actorRole: "guest",
            action: "PAYMENT_SUCCEEDED",
            resourceType: "PAYMENT_ORDER",
            resourceId: Guid.NewGuid(),
            details: "{}",
            occurredAt: DateTimeOffset.UtcNow);
        Assert.Equal("guest", guestLowerProj.ActorRole);

        // 2. Non-GUEST (e.g. DRIVER) with Guid.Empty actorId -> Throws NotificationDomainException
        var exDriver = Assert.Throws<NotificationDomainException>(() =>
            CentralAuditProjection.Create(
                originatingService: "PaymentService",
                eventId: Guid.NewGuid(),
                actorId: Guid.Empty,
                actorRole: "DRIVER",
                action: "PAYMENT_SUCCEEDED",
                resourceType: "PAYMENT_ORDER",
                resourceId: Guid.NewGuid(),
                details: "{}",
                occurredAt: DateTimeOffset.UtcNow));
        Assert.Contains("ActorId cannot be empty", exDriver.Message);

        // 3. Empty ActorRole -> Throws
        Assert.Throws<NotificationDomainException>(() =>
            CentralAuditProjection.Create(
                originatingService: "PaymentService",
                eventId: Guid.NewGuid(),
                actorId: Guid.Empty,
                actorRole: "   ",
                action: "PAYMENT_SUCCEEDED",
                resourceType: "PAYMENT_ORDER",
                resourceId: Guid.NewGuid(),
                details: "{}",
                occurredAt: DateTimeOffset.UtcNow));
    }

    #endregion

    #region 2. Registered Payment Processing, ICT Timezone (+07:00) & 15-Minute Grace Period Probes

    [Theory]
    [InlineData("2026-11-05T08:30:00+00:00", "15:45:00")] // 08:30 UTC + 15m = 08:45 UTC -> 15:45 ICT
    [InlineData("2026-11-05T16:50:00+00:00", "00:05:00")] // 16:50 UTC + 15m = 17:05 UTC -> 00:05 ICT (Midnight rollover)
    [InlineData("2026-11-05T23:55:00+07:00", "00:10:00")] // 23:55 ICT + 15m -> 00:10 ICT (Next day in local time)
    [InlineData("2026-02-28T23:50:00+00:00", "07:05:00")] // Month boundary
    public async Task RegisteredPayment_GracePeriodAndIctOffset_MustCalculateCorrectLocalExitDeadline(
        string occurredAtString,
        string expectedExitDeadline)
    {
        var handler = CreateHandler();
        var occurredAt = DateTimeOffset.Parse(occurredAtString);
        var eventId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var command = new ProcessPaymentNotificationCommand
        {
            EventId = eventId,
            OccurredAt = occurredAt,
            CorrelationId = "CORR-TZ-TEST",
            PaymentOrderId = orderId,
            PaymentTransactionId = Guid.NewGuid(),
            PaymentMethod = "PAYOS",
            TenantId = Guid.NewGuid(),
            SiteId = Guid.NewGuid(),
            PlateNumber = "51A-888.99",
            AccountId = accountId,
            Amount = 120000m,
            Currency = "VND"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(_notificationRepo.Notifications);

        var notif = _notificationRepo.Notifications[0];
        Assert.Equal(accountId, notif.RecipientId);
        Assert.Equal(NotificationChannel.IN_APP, notif.Channel);
        Assert.Equal("Thanh toán thành công", notif.Title);
        Assert.Equal("PAYMENT_SUCCESS", notif.TemplateCode);

        // Explicitly assert ICT (+07:00) calculated time and grace period note
        Assert.Contains($"rời bãi trước {expectedExitDeadline} (ân hạn 15 phút)", notif.Message);
        Assert.Contains("Biển số: 51A-888.99", notif.Message);
        Assert.Contains("Đã thanh toán: 120,000 VND qua PAYOS", notif.Message);

        // Verify JSON payload stores exitGraceCutoffAt
        var payloadDoc = JsonDocument.Parse(notif.Payload);
        Assert.True(payloadDoc.RootElement.TryGetProperty("exitGraceCutoffAt", out var graceProp));
        var graceUtc = graceProp.GetDateTimeOffset();
        Assert.Equal(occurredAt.UtcDateTime.AddMinutes(15), graceUtc.UtcDateTime);
    }

    [Fact]
    public async Task RegisteredPayment_WhenOccurredAtIsDefault_FallbackToUtcNowGracePeriod()
    {
        var handler = CreateHandler();
        var eventId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        var command = new ProcessPaymentNotificationCommand
        {
            EventId = eventId,
            OccurredAt = default, // Default/unspecified
            PaymentOrderId = Guid.NewGuid(),
            PaymentTransactionId = Guid.NewGuid(),
            PaymentMethod = "PAYOS",
            AccountId = accountId,
            Amount = 50000m,
            Currency = "VND"
        };

        var beforeCall = DateTimeOffset.UtcNow;
        var result = await handler.Handle(command, CancellationToken.None);
        var afterCall = DateTimeOffset.UtcNow;

        Assert.True(result.Success);
        Assert.Single(_notificationRepo.Notifications);
        var notif = _notificationRepo.Notifications[0];

        Assert.Contains("ân hạn 15 phút", notif.Message);
        Assert.Contains("Biển số: N/A", notif.Message); // null PlateNumber falls back to N/A
    }

    [Fact]
    public async Task RegisteredPayment_WhenPlateNumberIsNull_FormatsWithNA()
    {
        var handler = CreateHandler();
        var eventId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        var command = new ProcessPaymentNotificationCommand
        {
            EventId = eventId,
            OccurredAt = DateTimeOffset.UtcNow,
            PaymentOrderId = Guid.NewGuid(),
            PaymentTransactionId = Guid.NewGuid(),
            PaymentMethod = "PAYOS",
            PlateNumber = null,
            AccountId = accountId,
            Amount = 60000m,
            Currency = "VND"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.Success);
        var notif = _notificationRepo.Notifications[0];
        Assert.Contains("Biển số: N/A", notif.Message);
    }

    #endregion

    #region 3. Idempotency & Deduplication Probes

    [Fact]
    public async Task Deduplication_ReplayingRegisteredPaymentEvent_MustReturnDuplicateWithoutCreatingExtraRowsOrRedispatching()
    {
        var handler = CreateHandler();
        var eventId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var command = new ProcessPaymentNotificationCommand
        {
            EventId = eventId,
            PaymentOrderId = orderId,
            PaymentTransactionId = Guid.NewGuid(),
            PaymentMethod = "PAYOS",
            AccountId = accountId,
            Amount = 50000m,
            Currency = "VND",
            OccurredAt = DateTimeOffset.UtcNow
        };

        // First execution
        var result1 = await handler.Handle(command, CancellationToken.None);
        Assert.True(result1.Success);
        Assert.False(result1.IsDuplicate);
        Assert.Equal(1, _dispatcher.DispatchCalls);
        Assert.Single(_notificationRepo.Notifications);
        Assert.Single(_auditRepo.Projections);

        // Second execution (replayed duplicate event)
        var result2 = await handler.Handle(command, CancellationToken.None);
        Assert.True(result2.Success);
        Assert.True(result2.IsDuplicate);
        Assert.Equal("DUPLICATE_ACKNOWLEDGED", result2.Status);

        // Guard: Dispatcher must NOT be called a second time
        Assert.Equal(1, _dispatcher.DispatchCalls);

        // Guard: Exactly 1 notification and 1 audit projection in persistence
        Assert.Single(_notificationRepo.Notifications);
        Assert.Single(_auditRepo.Projections);

        // Third execution (stress duplicate replay)
        var result3 = await handler.Handle(command, CancellationToken.None);
        Assert.True(result3.IsDuplicate);
        Assert.Equal(1, _dispatcher.DispatchCalls);
        Assert.Single(_notificationRepo.Notifications);
        Assert.Single(_auditRepo.Projections);
    }

    [Fact]
    public async Task Deduplication_ReplayingGuestPaymentEvent_MustReturnDuplicateWithoutExtraAudit()
    {
        var handler = CreateHandler();
        var eventId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var command = new ProcessPaymentNotificationCommand
        {
            EventId = eventId,
            PaymentOrderId = orderId,
            PaymentTransactionId = Guid.NewGuid(),
            PaymentMethod = "CASH",
            AccountId = null, // Guest
            Amount = 20000m,
            Currency = "VND",
            OccurredAt = DateTimeOffset.UtcNow
        };

        // First execution
        var r1 = await handler.Handle(command, CancellationToken.None);
        Assert.True(r1.Success);
        Assert.False(r1.IsDuplicate);
        Assert.Empty(_notificationRepo.Notifications);
        Assert.Single(_auditRepo.Projections);

        // Replay
        var r2 = await handler.Handle(command, CancellationToken.None);
        Assert.True(r2.Success);
        Assert.True(r2.IsDuplicate);
        Assert.Empty(_notificationRepo.Notifications);
        Assert.Single(_auditRepo.Projections);
    }

    [Fact]
    public async Task Deduplication_WhenAuditAlreadyExistsExternally_AcknowledgeDuplicateEarly()
    {
        var eventId = Guid.NewGuid();
        // Simulate audit projection already written by external pipeline or previous transaction
        var existingAudit = CentralAuditProjection.Create(
            "PaymentService",
            eventId,
            Guid.Empty,
            "GUEST",
            "PAYMENT_SUCCEEDED",
            "PAYMENT_ORDER",
            Guid.NewGuid(),
            "{}",
            DateTimeOffset.UtcNow);
        await _auditRepo.AddAsync(existingAudit);

        var handler = CreateHandler();
        var command = new ProcessPaymentNotificationCommand
        {
            EventId = eventId,
            PaymentOrderId = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            Amount = 50000m,
            Currency = "VND"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsDuplicate);
        Assert.Empty(_notificationRepo.Notifications);
        Assert.Equal(0, _dispatcher.DispatchCalls);
    }

    [Fact]
    public async Task Deduplication_WhenNotificationKeyAlreadyExists_ReturnsDuplicateWithoutInserting()
    {
        var eventId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var idempotencyKey = $"PAY_{eventId}_{recipientId}";

        var existingNotification = DurableNotification.Create(
            "PaymentSucceededV1",
            idempotencyKey,
            recipientId,
            NotificationChannel.IN_APP,
            "PAYMENT_SUCCESS",
            "{}",
            title: "Old title",
            message: "Old msg");
        await _notificationRepo.AddAsync(existingNotification);

        var handler = CreateHandler();
        var command = new ProcessPaymentNotificationCommand
        {
            EventId = eventId,
            AccountId = recipientId,
            PaymentOrderId = Guid.NewGuid(),
            Amount = 50000m,
            Currency = "VND"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsDuplicate);
        Assert.Equal(existingNotification.Id, result.NotificationId);
        Assert.Single(_notificationRepo.Notifications);
        Assert.Equal(0, _dispatcher.DispatchCalls);
    }

    [Fact]
    public async Task MultipleDistinctPayments_ForSameRecipient_MustProcessBothIndependently()
    {
        var handler = CreateHandler();
        var recipientId = Guid.NewGuid();

        var event1 = Guid.NewGuid();
        var event2 = Guid.NewGuid();

        var command1 = new ProcessPaymentNotificationCommand
        {
            EventId = event1,
            PaymentOrderId = Guid.NewGuid(),
            PaymentTransactionId = Guid.NewGuid(),
            AccountId = recipientId,
            Amount = 30000m,
            Currency = "VND",
            PaymentMethod = "PAYOS"
        };

        var command2 = new ProcessPaymentNotificationCommand
        {
            EventId = event2,
            PaymentOrderId = Guid.NewGuid(),
            PaymentTransactionId = Guid.NewGuid(),
            AccountId = recipientId,
            Amount = 70000m,
            Currency = "VND",
            PaymentMethod = "CASH"
        };

        var r1 = await handler.Handle(command1, CancellationToken.None);
        var r2 = await handler.Handle(command2, CancellationToken.None);

        Assert.True(r1.Success && !r1.IsDuplicate);
        Assert.True(r2.Success && !r2.IsDuplicate);

        Assert.Equal(2, _notificationRepo.Notifications.Count);
        Assert.Equal(2, _auditRepo.Projections.Count);
        Assert.Equal(2, _dispatcher.DispatchCalls);
    }

    #endregion
}
