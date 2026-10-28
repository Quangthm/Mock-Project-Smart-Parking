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
    public async Task PaymentConsumer_WhenEventPublished_ShouldPersistDurableNotificationAndDeliver()
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
            var driverId = Guid.NewGuid();
            var orderId = Guid.NewGuid();

            var message = new PaymentVerifiedNotificationEvent
            {
                EventId = eventId,
                PaymentOrderId = orderId,
                DriverId = driverId,
                Amount = 75000m,
                Currency = "VND",
                PaymentMethod = "PAYOS",
                TransactionId = "TXN_789012",
                OccurredAt = DateTimeOffset.UtcNow
            };

            await harness.Bus.Publish(message);

            var consumerHarness = harness.GetConsumerHarness<PaymentNotificationConsumer>();
            Assert.True(await consumerHarness.Consumed.Any<PaymentVerifiedNotificationEvent>());

            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NotificationAuditDbContext>();
            var saved = await db.DurableNotifications.FirstOrDefaultAsync(n => n.RecipientId == driverId);

            Assert.NotNull(saved);
            Assert.Equal(NotificationStatus.DELIVERED, saved.Status);
            Assert.Equal($"PAY_{eventId}_{driverId}", saved.IdempotencyKey);
            Assert.Equal("PAYMENT_SUCCESS", saved.TemplateCode);
        }
        finally
        {
            await harness.Stop();
        }
    }
}
