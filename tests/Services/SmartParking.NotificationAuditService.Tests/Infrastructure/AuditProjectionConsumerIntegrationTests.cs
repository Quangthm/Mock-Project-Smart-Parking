using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.NotificationAuditService.Application;
using SmartParking.NotificationAuditService.Application.Common.Interfaces.Persistence;
using SmartParking.NotificationAuditService.Application.Contracts.Audit;
using SmartParking.NotificationAuditService.Infrastructure.Consumers;
using SmartParking.NotificationAuditService.Persistence.Context;
using SmartParking.NotificationAuditService.Persistence.Repositories;

namespace SmartParking.NotificationAuditService.Tests.Infrastructure;

public class AuditProjectionConsumerIntegrationTests
{
    [Fact]
    public async Task Consumer_WhenAuditProjectionEventReceived_ShouldPersistProjection()
    {
        var services = new ServiceCollection();

        // 1. In-memory DbContext
        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<NotificationAuditDbContext>(opt =>
            opt.UseInMemoryDatabase(dbName));

        // 2. Application & Persistence
        services.AddLogging();
        services.AddApplicationServices();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<INotificationTemplateRepository, NotificationTemplateRepository>();
        services.AddScoped<ICentralAuditProjectionRepository, CentralAuditProjectionRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // 3. MassTransit Test Harness (Synthetic Broker)
        services.AddMassTransitTestHarness(cfg =>
        {
            cfg.AddConsumer<AuditProjectionConsumer>();
        });

        var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<ITestHarness>();

        await harness.Start();
        try
        {
            var eventId = Guid.NewGuid();
            var syntheticEvent = new AuditProjectionEvent
            {
                EventId = eventId,
                OriginatingService = "ReservationService",
                ActorId = Guid.NewGuid(),
                ActorRole = "Driver",
                Action = "ReservationCancelled",
                ResourceType = "Reservation",
                ResourceId = Guid.NewGuid(),
                Details = "{\"reason\": \"Changed mind\"}",
                OccurredAt = DateTimeOffset.UtcNow
            };

            // Synthetic Producer: publish through broker harness
            await harness.Bus.Publish(syntheticEvent);

            // Assert: MassTransit Consumer consumed message
            var consumerHarness = harness.GetConsumerHarness<AuditProjectionConsumer>();
            Assert.True(await consumerHarness.Consumed.Any<AuditProjectionEvent>());

            // Assert: Database has persisted the central audit projection
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NotificationAuditDbContext>();
            var projection = await db.CentralAuditProjections.FirstOrDefaultAsync(p => p.EventId == eventId);

            Assert.NotNull(projection);
            Assert.Equal("ReservationService", projection.OriginatingService);
            Assert.Equal("ReservationCancelled", projection.Action);
            Assert.Equal("Driver", projection.ActorRole);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Consumer_WhenDuplicateEventReceived_ShouldDeduplicateWithoutCreatingSecondRow()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<NotificationAuditDbContext>(opt =>
            opt.UseInMemoryDatabase(dbName));

        services.AddLogging();
        services.AddApplicationServices();
        services.AddScoped<ICentralAuditProjectionRepository, CentralAuditProjectionRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddMassTransitTestHarness(cfg =>
        {
            cfg.AddConsumer<AuditProjectionConsumer>();
        });

        var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<ITestHarness>();

        await harness.Start();
        try
        {
            var eventId = Guid.NewGuid();
            var syntheticEvent = new AuditProjectionEvent
            {
                EventId = eventId,
                OriginatingService = "PaymentService",
                ActorId = Guid.NewGuid(),
                ActorRole = "System",
                Action = "PaymentCompleted",
                ResourceType = "PaymentTransaction",
                ResourceId = Guid.NewGuid(),
                Details = "{\"status\": \"SUCCESS\"}",
                OccurredAt = DateTimeOffset.UtcNow
            };

            // First publish
            await harness.Bus.Publish(syntheticEvent);

            var consumerHarness = harness.GetConsumerHarness<AuditProjectionConsumer>();
            Assert.True(await consumerHarness.Consumed.Any<AuditProjectionEvent>());

            // Second publish of identical event (Simulate Broker replay)
            await harness.Bus.Publish(syntheticEvent);

            // Give consumer time to process replay
            await Task.Delay(300);

            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NotificationAuditDbContext>();
            var count = await db.CentralAuditProjections.CountAsync(p => p.EventId == eventId);

            // Must strictly be 1! (Durable deduplication preserved)
            Assert.Equal(1, count);
        }
        finally
        {
            await harness.Stop();
        }
    }
}
