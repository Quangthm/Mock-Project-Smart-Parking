using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.NotificationAuditService.Application;
using SmartParking.NotificationAuditService.Application.Common.Interfaces.Persistence;
using SmartParking.NotificationAuditService.Application.Common.Interfaces.Ports;
using SmartParking.NotificationAuditService.Application.Contracts.Notifications;
using SmartParking.NotificationAuditService.Domain.Enums;
using SmartParking.NotificationAuditService.Infrastructure.Adapters.Delivery;
using SmartParking.NotificationAuditService.Infrastructure.Consumers;
using SmartParking.NotificationAuditService.Persistence.Context;
using SmartParking.NotificationAuditService.Persistence.Repositories;
using SmartParking.NotificationAuditService.Persistence.Seed;
using SmartParking.PaymentService.Domain.Events;

namespace SmartParking.NotificationAuditService.Tests.Infrastructure;

public class NotificationConsumersIntegrationTests
{
    [Fact]
    public async Task ReservationConsumer_WhenEventPublished_ShouldPersistDurableNotificationAndDeliver()
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
            cfg.AddConsumer<ReservationNotificationConsumer>();
        });

        var provider = services.BuildServiceProvider();

        // Seed templates
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
            var driverId = Guid.NewGuid();
            var reservationId = Guid.NewGuid();

            var message = new ReservationConfirmedNotificationEvent
            {
                EventId = eventId,
                ReservationId = reservationId,
                DriverId = driverId,
                SiteId = Guid.NewGuid(),
                SiteName = "Central Lot",
                SlotCode = "C-01",
                StartTime = DateTimeOffset.UtcNow,
                EndTime = DateTimeOffset.UtcNow.AddHours(2),
                OccurredAt = DateTimeOffset.UtcNow
            };

            await harness.Bus.Publish(message);

            var consumerHarness = harness.GetConsumerHarness<ReservationNotificationConsumer>();
            Assert.True(await consumerHarness.Consumed.Any<ReservationConfirmedNotificationEvent>());

            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NotificationAuditDbContext>();
            var saved = await db.DurableNotifications.FirstOrDefaultAsync(n => n.RecipientId == driverId);

            Assert.NotNull(saved);
            Assert.Equal(NotificationStatus.DELIVERED, saved.Status);
            Assert.Equal($"RES_{eventId}_{driverId}", saved.IdempotencyKey);
            Assert.Equal("RESERVATION_CONFIRMED", saved.TemplateCode);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task PaymentConsumer_WhenRegisteredPaymentPublished_ShouldPersistDurableNotificationAndAudit()
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
                CorrelationId = "CORR-001",
                PaymentOrderId = orderId,
                PaymentTransactionId = Guid.NewGuid(),
                PaymentMethod = "PAYOS",
                GatewayReference = "PAYOS-12345",
                TenantId = Guid.NewGuid(),
                SiteId = Guid.NewGuid(),
                ReferenceType = "PARKING_SESSION",
                ReferenceId = Guid.NewGuid(),
                PlateNumber = "51A-123.45",
                AccountId = accountId,
                Amount = 75000m,
                ParkingFee = 75000m,
                PenaltyFee = null,
                PenaltyReason = null,
                Currency = "VND"
            };

            await harness.Bus.Publish(message);

            var consumerHarness = harness.GetConsumerHarness<PaymentNotificationConsumer>();
            Assert.True(await consumerHarness.Consumed.Any<PaymentSucceededV1>());

            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NotificationAuditDbContext>();

            // 1. DurableNotification must exist
            var saved = await db.DurableNotifications.FirstOrDefaultAsync(n => n.RecipientId == accountId);
            Assert.NotNull(saved);
            Assert.Equal(NotificationStatus.DELIVERED, saved.Status);
            Assert.Equal($"PAY_{eventId}_{accountId}", saved.IdempotencyKey);
            Assert.Equal("PAYMENT_SUCCESS", saved.TemplateCode);
            Assert.Equal("Thanh toán thành công", saved.Title);
            Assert.Contains("51A-123.45", saved.Message);
            Assert.Contains("75,000 VND", saved.Message);
            Assert.Contains("ân hạn 15 phút", saved.Message);

            // 2. CentralAuditProjection must exist
            var audit = await db.CentralAuditProjections.FirstOrDefaultAsync(p => p.EventId == eventId);
            Assert.NotNull(audit);
            Assert.Equal("PaymentService", audit.OriginatingService);
            Assert.Equal("PAYMENT_SUCCEEDED", audit.Action);
            Assert.Equal("DRIVER", audit.ActorRole);
            Assert.Equal(accountId, audit.ActorId);
            Assert.Equal(orderId, audit.ResourceId);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task PaymentConsumer_WhenGuestPaymentPublished_ShouldPersistAuditProjectionAndNoNotification()
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
            var orderId = Guid.NewGuid();

            var message = new PaymentSucceededV1
            {
                EventId = eventId,
                OccurredAt = DateTimeOffset.UtcNow,
                CorrelationId = "CORR-GUEST-001",
                PaymentOrderId = orderId,
                PaymentTransactionId = Guid.NewGuid(),
                PaymentMethod = "CASH",
                GatewayReference = null,
                TenantId = Guid.NewGuid(),
                SiteId = Guid.NewGuid(),
                ReferenceType = "PARKING_SESSION",
                ReferenceId = Guid.NewGuid(),
                PlateNumber = "59-B1 678.90",
                AccountId = null, // GUEST
                Amount = 20000m,
                ParkingFee = 20000m,
                PenaltyFee = null,
                PenaltyReason = null,
                Currency = "VND"
            };

            await harness.Bus.Publish(message);

            var consumerHarness = harness.GetConsumerHarness<PaymentNotificationConsumer>();
            Assert.True(await consumerHarness.Consumed.Any<PaymentSucceededV1>());

            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NotificationAuditDbContext>();

            // 1. No DurableNotification should be created for Guest
            var count = await db.DurableNotifications.CountAsync();
            Assert.Equal(0, count);

            // 2. CentralAuditProjection must exist with ActorRole = GUEST
            var audit = await db.CentralAuditProjections.FirstOrDefaultAsync(p => p.EventId == eventId);
            Assert.NotNull(audit);
            Assert.Equal("PaymentService", audit.OriginatingService);
            Assert.Equal("PAYMENT_SUCCEEDED", audit.Action);
            Assert.Equal("GUEST", audit.ActorRole);
            Assert.Equal(Guid.Empty, audit.ActorId);
            Assert.Equal(orderId, audit.ResourceId);
        }
        finally
        {
            await harness.Stop();
        }
    }
}
