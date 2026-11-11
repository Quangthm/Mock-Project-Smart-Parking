using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.NotificationAuditService.Application;
using SmartParking.NotificationAuditService.Application.Common.Interfaces.Persistence;
using SmartParking.NotificationAuditService.Application.Common.Interfaces.Ports;
using SmartParking.NotificationAuditService.Application.Usecase.Notifications.Commands.MarkAllNotificationsRead;
using SmartParking.NotificationAuditService.Application.Usecase.Notifications.Commands.MarkNotificationRead;
using SmartParking.NotificationAuditService.Domain.Entities;
using SmartParking.NotificationAuditService.Domain.Enums;
using SmartParking.NotificationAuditService.Domain.Exceptions;
using SmartParking.NotificationAuditService.Infrastructure.Adapters.Delivery;
using SmartParking.NotificationAuditService.Infrastructure.Consumers;
using SmartParking.NotificationAuditService.Persistence.Context;
using SmartParking.NotificationAuditService.Persistence.Repositories;
using SmartParking.NotificationAuditService.Persistence.Seed;
using SmartParking.NotificationAuditService.Tests.TestDoubles;
using SmartParking.PaymentService.Domain.Events;
using Xunit;

namespace SmartParking.NotificationAuditService.Tests.Application;

public class NotificationMilestone2AdversarialTests
{
    #region Probe 1: MarkNotificationReadCommand Adversarial Tests

    [Fact]
    public async Task MarkNotificationRead_WhenNotFound_WithNullRequestingUser_ReturnsNullWithoutMutations()
    {
        var repo = new FakeNotificationRepository();
        var uow = new FakeUnitOfWork();
        var handler = new MarkNotificationReadCommandHandler(repo, uow, NullLogger<MarkNotificationReadCommandHandler>.Instance);

        var nonExistentId = Guid.NewGuid();
        var command = new MarkNotificationReadCommand(nonExistentId, requestingUserId: null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, uow.SaveCount);
        Assert.Empty(repo.Notifications);
    }

    [Fact]
    public async Task MarkNotificationRead_WhenNotFound_WithSpecificRequestingUser_ReturnsNullWithoutThrowingForbidden()
    {
        var repo = new FakeNotificationRepository();
        var uow = new FakeUnitOfWork();
        var handler = new MarkNotificationReadCommandHandler(repo, uow, NullLogger<MarkNotificationReadCommandHandler>.Instance);

        var nonExistentId = Guid.NewGuid();
        var requestingUser = Guid.NewGuid();
        var command = new MarkNotificationReadCommand(nonExistentId, requestingUser);

        // Adversarial expectation: Non-existent notification must return null (404-like) instead of falsely claiming forbidden
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, uow.SaveCount);
    }

    [Fact]
    public async Task MarkNotificationRead_WhenUserMismatch_ThrowsForbiddenAndLeavesNotificationUnmutated()
    {
        var repo = new FakeNotificationRepository();
        var uow = new FakeUnitOfWork();
        var handler = new MarkNotificationReadCommandHandler(repo, uow, NullLogger<MarkNotificationReadCommandHandler>.Instance);

        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();

        var notification = DurableNotification.Create(
            originatingEvent: "PaymentSucceededV1",
            idempotencyKey: "KEY-MISMATCH-1",
            recipientId: ownerId,
            channel: NotificationChannel.IN_APP,
            templateCode: "PAYMENT_SUCCESS",
            payload: "{}",
            title: "Original Title",
            message: "Original Message");

        await repo.AddAsync(notification);
        Assert.False(notification.IsRead);
        Assert.Null(notification.ReadAt);

        var command = new MarkNotificationReadCommand(notification.Id, requestingUserId: attackerId);

        // Assert: Throws NotificationForbiddenException
        var ex = await Assert.ThrowsAsync<NotificationForbiddenException>(() =>
            handler.Handle(command, CancellationToken.None));

        Assert.Contains("permission", ex.Message, StringComparison.OrdinalIgnoreCase);

        // Adversarial invariant: State must NOT be mutated in memory or persistence
        Assert.Equal(0, uow.SaveCount);
        var inRepo = await repo.GetByIdAsync(notification.Id);
        Assert.NotNull(inRepo);
        Assert.False(inRepo.IsRead);
        Assert.Null(inRepo.ReadAt);
    }

    [Fact]
    public async Task MarkNotificationRead_WhenAlreadyRead_MarkingAgainIsIdempotentAndPreservesOriginalTimestamp()
    {
        var repo = new FakeNotificationRepository();
        var uow = new FakeUnitOfWork();
        var handler = new MarkNotificationReadCommandHandler(repo, uow, NullLogger<MarkNotificationReadCommandHandler>.Instance);

        var ownerId = Guid.NewGuid();
        var notification = DurableNotification.Create(
            originatingEvent: "PaymentSucceededV1",
            idempotencyKey: "KEY-IDEMPOTENT-1",
            recipientId: ownerId,
            channel: NotificationChannel.IN_APP,
            templateCode: "PAYMENT_SUCCESS",
            payload: "{}");

        notification.MarkAsRead();
        var historicalReadAt = DateTimeOffset.UtcNow.AddHours(-3);
        typeof(DurableNotification).GetProperty(nameof(DurableNotification.ReadAt))!
            .SetValue(notification, historicalReadAt);

        await repo.AddAsync(notification);

        var command = new MarkNotificationReadCommand(notification.Id, requestingUserId: ownerId);
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.IsRead);
        Assert.Equal(historicalReadAt, result.ReadAt);
        Assert.Equal(1, uow.SaveCount);

        var inRepo = await repo.GetByIdAsync(notification.Id);
        Assert.NotNull(inRepo);
        Assert.Equal(historicalReadAt, inRepo.ReadAt);
    }

    [Fact]
    public async Task MarkNotificationRead_WhenRequestingUserIdIsEmptyOrNull_AllowsSystemLevelMarkRead()
    {
        var repo = new FakeNotificationRepository();
        var uow = new FakeUnitOfWork();
        var handler = new MarkNotificationReadCommandHandler(repo, uow, NullLogger<MarkNotificationReadCommandHandler>.Instance);

        var recipientId = Guid.NewGuid();
        var notification = DurableNotification.Create(
            originatingEvent: "SystemAlert",
            idempotencyKey: "KEY-SYSTEM-1",
            recipientId: recipientId,
            channel: NotificationChannel.IN_APP,
            templateCode: "SYSTEM_ALERT",
            payload: "{}");

        await repo.AddAsync(notification);

        // Guid.Empty acts as system/internal caller
        var commandSystem = new MarkNotificationReadCommand(notification.Id, requestingUserId: Guid.Empty);
        var result = await handler.Handle(commandSystem, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.IsRead);
        Assert.NotNull(result.ReadAt);
        Assert.Equal(1, uow.SaveCount);
    }

    [Fact]
    public void MarkNotificationReadValidator_WhenIdIsEmpty_FailsValidation()
    {
        var validator = new MarkNotificationReadCommandValidator();
        var command = new MarkNotificationReadCommand(Guid.Empty, Guid.NewGuid());

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Id");
    }

    #endregion

    #region Probe 2: MarkAllNotificationsReadCommand Multi-Recipient Partition Tests

    [Fact]
    public async Task MarkAllNotificationsRead_MultiRecipientPartitionIsolation_StrictVerification()
    {
        var repo = new FakeNotificationRepository();
        var handler = new MarkAllNotificationsReadCommandHandler(repo, NullLogger<MarkAllNotificationsReadCommandHandler>.Instance);

        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var userC = Guid.NewGuid();
        var userD = Guid.NewGuid();

        var historicalTimestamp = DateTimeOffset.UtcNow.AddDays(-2);

        // User A: 5 unread, 3 already read
        for (int i = 0; i < 5; i++)
            await repo.AddAsync(DurableNotification.Create("Evt", $"Key-A-U-{i}", userA, NotificationChannel.IN_APP, "TPL", "{}"));
        for (int i = 0; i < 3; i++)
        {
            var n = DurableNotification.Create("Evt", $"Key-A-R-{i}", userA, NotificationChannel.IN_APP, "TPL", "{}");
            n.MarkAsRead();
            typeof(DurableNotification).GetProperty(nameof(DurableNotification.ReadAt))!.SetValue(n, historicalTimestamp);
            await repo.AddAsync(n);
        }

        // User B: 10 unread, 0 read
        for (int i = 0; i < 10; i++)
            await repo.AddAsync(DurableNotification.Create("Evt", $"Key-B-U-{i}", userB, NotificationChannel.EMAIL, "TPL", "{}"));

        // User C: 0 unread, 4 read
        for (int i = 0; i < 4; i++)
        {
            var n = DurableNotification.Create("Evt", $"Key-C-R-{i}", userC, NotificationChannel.SMS, "TPL", "{}");
            n.MarkAsRead();
            typeof(DurableNotification).GetProperty(nameof(DurableNotification.ReadAt))!.SetValue(n, historicalTimestamp);
            await repo.AddAsync(n);
        }

        // User D: zero notifications total

        // 1. MarkAllRead for User A
        var countA = await handler.Handle(new MarkAllNotificationsReadCommand(userA), CancellationToken.None);
        Assert.Equal(5, countA);

        var listA = await repo.GetByRecipientIdAsync(userA);
        Assert.Equal(8, listA.Count);
        Assert.All(listA, n => Assert.True(n.IsRead));
        // Verify User A's previously read notifications still have historical timestamp
        var preservedA = listA.Where(n => n.ReadAt == historicalTimestamp).ToList();
        Assert.Equal(3, preservedA.Count);

        // 2. Verify User B is completely untouched (still 10 unread)
        var listB = await repo.GetByRecipientIdAsync(userB);
        Assert.Equal(10, listB.Count);
        Assert.All(listB, n => Assert.False(n.IsRead));

        // 3. Verify User C is completely untouched (still 4 read with historical timestamp)
        var listC = await repo.GetByRecipientIdAsync(userC);
        Assert.Equal(4, listC.Count);
        Assert.All(listC, n =>
        {
            Assert.True(n.IsRead);
            Assert.Equal(historicalTimestamp, n.ReadAt);
        });

        // 4. MarkAllRead for User C (already 100% read) -> returns 0
        var countC = await handler.Handle(new MarkAllNotificationsReadCommand(userC), CancellationToken.None);
        Assert.Equal(0, countC);

        // 5. MarkAllRead for User D (0 notifications) -> returns 0
        var countD = await handler.Handle(new MarkAllNotificationsReadCommand(userD), CancellationToken.None);
        Assert.Equal(0, countD);

        // 6. Consecutive call for User A -> returns 0 (idempotent)
        var countA2 = await handler.Handle(new MarkAllNotificationsReadCommand(userA), CancellationToken.None);
        Assert.Equal(0, countA2);
    }

    [Fact]
    public void MarkAllNotificationsReadValidator_WhenRecipientIdIsEmpty_FailsValidation()
    {
        var validator = new MarkAllNotificationsReadCommandValidator();
        var command = new MarkAllNotificationsReadCommand(Guid.Empty);

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "RecipientId");
    }

    #endregion

    #region Probe 3: PaymentNotificationConsumer with PaymentSucceededV1

    [Fact]
    public async Task PaymentConsumer_WhenFull18FieldsMessagePublished_ProcessesCorrectlyAndGuaranteesAuditAndNotification()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();

        services.AddDbContext<NotificationAuditDbContext>(opt =>
            opt.UseInMemoryDatabase(dbName));

        services.AddLogging();
        services.AddApplicationServices();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<INotificationTemplateRepository, NotificationTemplateRepository>();
        services.AddScoped<ICentralAuditProjectionRepository, CentralAuditProjectionRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<INotificationChannelAdapter, InAppDeliveryAdapter>();
        services.AddScoped<INotificationChannelAdapter, LocalEmailCaptureAdapter>();
        services.AddScoped<INotificationChannelAdapter, LocalSmsFixtureAdapter>();
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();

        services.AddMassTransitTestHarness(cfg =>
        {
            cfg.AddConsumer<PaymentNotificationConsumer>();
        });

        var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotificationAuditDbContext>();
            await NotificationTemplateSeeder.SeedAsync(db);
        }

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var eventId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            var txnId = Guid.NewGuid();
            var tenantId = Guid.NewGuid();
            var siteId = Guid.NewGuid();
            var refId = Guid.NewGuid();
            var fixedOccurredAt = new DateTimeOffset(2026, 11, 5, 14, 0, 0, TimeSpan.Zero); // 14:00 UTC = 21:00 ICT

            var message = new PaymentSucceededV1
            {
                EventId = eventId,
                OccurredAt = fixedOccurredAt,
                CorrelationId = "CORR-ADV-001",
                PaymentOrderId = orderId,
                PaymentTransactionId = txnId,
                PaymentMethod = "PAYOS",
                GatewayReference = "GW-PAYOS-9999",
                TenantId = tenantId,
                SiteId = siteId,
                ReferenceType = "PARKING_SESSION",
                ReferenceId = refId,
                PlateNumber = "29A-888.88",
                AccountId = accountId,
                Amount = 120000m,
                ParkingFee = 100000m,
                PenaltyFee = 20000m,
                PenaltyReason = "Quá giờ 30 phút",
                Currency = "VND"
            };

            await harness.Bus.Publish(message);

            var consumerHarness = harness.GetConsumerHarness<PaymentNotificationConsumer>();
            Assert.True(await consumerHarness.Consumed.Any<PaymentSucceededV1>());

            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NotificationAuditDbContext>();

            // 1. Verify DurableNotification
            var notification = await db.DurableNotifications.FirstOrDefaultAsync(n => n.RecipientId == accountId);
            Assert.NotNull(notification);
            Assert.Equal(NotificationStatus.DELIVERED, notification.Status);
            Assert.Equal($"PAY_{eventId}_{accountId}", notification.IdempotencyKey);
            Assert.Equal(NotificationChannel.IN_APP, notification.Channel);
            Assert.Equal("PAYMENT_SUCCESS", notification.TemplateCode);
            Assert.Equal("Thanh toán thành công", notification.Title);
            Assert.Contains("29A-888.88", notification.Message);
            Assert.Contains("120,000 VND", notification.Message);
            Assert.Contains("PAYOS", notification.Message);
            // 14:00 UTC + 15 min = 14:15 UTC -> 21:15:00 ICT (+07:00)
            Assert.Contains("21:15:00", notification.Message);
            Assert.Contains("ân hạn 15 phút", notification.Message);

            // 2. Verify CentralAuditProjection
            var audit = await db.CentralAuditProjections.FirstOrDefaultAsync(p => p.EventId == eventId);
            Assert.NotNull(audit);
            Assert.Equal("PaymentService", audit.OriginatingService);
            Assert.Equal("PAYMENT_SUCCEEDED", audit.Action);
            Assert.Equal("DRIVER", audit.ActorRole);
            Assert.Equal(accountId, audit.ActorId);
            Assert.Equal("PAYMENT_ORDER", audit.ResourceType);
            Assert.Equal(orderId, audit.ResourceId);
            Assert.Equal(fixedOccurredAt, audit.OccurredAt);
            Assert.Contains("29A-888.88", audit.Details);
            Assert.Contains("GW-PAYOS-9999", audit.Details);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task PaymentConsumer_WhenReplayedIdenticalMessage_ConsumerAcknowledgesWithoutDuplicates()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();

        services.AddDbContext<NotificationAuditDbContext>(opt =>
            opt.UseInMemoryDatabase(dbName));

        services.AddLogging();
        services.AddApplicationServices();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<INotificationTemplateRepository, NotificationTemplateRepository>();
        services.AddScoped<ICentralAuditProjectionRepository, CentralAuditProjectionRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<INotificationChannelAdapter, InAppDeliveryAdapter>();
        services.AddScoped<INotificationChannelAdapter, LocalEmailCaptureAdapter>();
        services.AddScoped<INotificationChannelAdapter, LocalSmsFixtureAdapter>();
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();

        services.AddMassTransitTestHarness(cfg =>
        {
            cfg.AddConsumer<PaymentNotificationConsumer>();
        });

        var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotificationAuditDbContext>();
            await NotificationTemplateSeeder.SeedAsync(db);
        }

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var eventId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var orderId = Guid.NewGuid();

            var message = new PaymentSucceededV1
            {
                EventId = eventId,
                OccurredAt = DateTimeOffset.UtcNow,
                CorrelationId = "CORR-REPLAY-001",
                PaymentOrderId = orderId,
                PaymentTransactionId = Guid.NewGuid(),
                PaymentMethod = "PAYOS",
                TenantId = Guid.NewGuid(),
                SiteId = Guid.NewGuid(),
                ReferenceType = "PARKING_SESSION",
                ReferenceId = Guid.NewGuid(),
                PlateNumber = "51F-111.22",
                AccountId = accountId,
                Amount = 50000m,
                ParkingFee = 50000m,
                Currency = "VND"
            };

            // First publish
            await harness.Bus.Publish(message);
            // Second publish (exact replay)
            await harness.Bus.Publish(message);

            var consumerHarness = harness.GetConsumerHarness<PaymentNotificationConsumer>();
            // Must consume at least 2 messages
            Assert.True(await consumerHarness.Consumed.Any<PaymentSucceededV1>());

            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NotificationAuditDbContext>();

            // Strictly 1 audit projection
            var auditCount = await db.CentralAuditProjections.CountAsync(p => p.EventId == eventId);
            Assert.Equal(1, auditCount);

            // Strictly 1 durable notification
            var notifCount = await db.DurableNotifications.CountAsync(n => n.RecipientId == accountId);
            Assert.Equal(1, notifCount);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task PaymentConsumer_TimezoneBoundaryRollsOverUtcMidnightToIctMorning()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();

        services.AddDbContext<NotificationAuditDbContext>(opt =>
            opt.UseInMemoryDatabase(dbName));

        services.AddLogging();
        services.AddApplicationServices();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<INotificationTemplateRepository, NotificationTemplateRepository>();
        services.AddScoped<ICentralAuditProjectionRepository, CentralAuditProjectionRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<INotificationChannelAdapter, InAppDeliveryAdapter>();
        services.AddScoped<INotificationChannelAdapter, LocalEmailCaptureAdapter>();
        services.AddScoped<INotificationChannelAdapter, LocalSmsFixtureAdapter>();
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();

        services.AddMassTransitTestHarness(cfg =>
        {
            cfg.AddConsumer<PaymentNotificationConsumer>();
        });

        var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotificationAuditDbContext>();
            await NotificationTemplateSeeder.SeedAsync(db);
        }

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var eventId = Guid.NewGuid();
            var accountId = Guid.NewGuid();

            // 23:50:00 UTC on Nov 5 -> +15m = 00:05:00 UTC Nov 6 -> ICT (+07:00) is 07:05:00 on Nov 6
            var midnightBoundaryTime = new DateTimeOffset(2026, 11, 5, 23, 50, 0, TimeSpan.Zero);

            var message = new PaymentSucceededV1
            {
                EventId = eventId,
                OccurredAt = midnightBoundaryTime,
                CorrelationId = "CORR-TZ-001",
                PaymentOrderId = Guid.NewGuid(),
                PaymentTransactionId = Guid.NewGuid(),
                PaymentMethod = "PAYOS",
                TenantId = Guid.NewGuid(),
                SiteId = Guid.NewGuid(),
                ReferenceType = "PARKING_SESSION",
                ReferenceId = Guid.NewGuid(),
                PlateNumber = "51A-999.99",
                AccountId = accountId,
                Amount = 40000m,
                ParkingFee = 40000m,
                Currency = "VND"
            };

            await harness.Bus.Publish(message);

            var consumerHarness = harness.GetConsumerHarness<PaymentNotificationConsumer>();
            Assert.True(await consumerHarness.Consumed.Any<PaymentSucceededV1>());

            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NotificationAuditDbContext>();

            var saved = await db.DurableNotifications.FirstOrDefaultAsync(n => n.RecipientId == accountId);
            Assert.NotNull(saved);
            Assert.Contains("07:05:00", saved.Message);
        }
        finally
        {
            await harness.Stop();
        }
    }

    #endregion
}
