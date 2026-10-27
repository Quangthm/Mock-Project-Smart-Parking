using Microsoft.EntityFrameworkCore;
using SmartParking.NotificationAuditService.Domain.Entities;
using SmartParking.NotificationAuditService.Domain.Enums;
using SmartParking.NotificationAuditService.Persistence.Context;

namespace SmartParking.NotificationAuditService.Tests.Persistence;

public class DbContextMappingTests
{
    [Fact]
    public void ModelConfiguration_ShouldMapExpectedTablesAndProperties()
    {
        var options = new DbContextOptionsBuilder<NotificationAuditDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new NotificationAuditDbContext(options);
        var model = context.Model;

        // 1. notification_templates
        var templateEntity = model.FindEntityType(typeof(NotificationTemplate));
        Assert.NotNull(templateEntity);
        Assert.Equal("notification_templates", templateEntity.GetTableName());

        // 2. durable_notifications
        var notificationEntity = model.FindEntityType(typeof(DurableNotification));
        Assert.NotNull(notificationEntity);
        Assert.Equal("durable_notifications", notificationEntity.GetTableName());

        // 3. central_audit_projections
        var auditEntity = model.FindEntityType(typeof(CentralAuditProjection));
        Assert.NotNull(auditEntity);
        Assert.Equal("central_audit_projections", auditEntity.GetTableName());
    }

    [Fact]
    public async Task CanAddAndRetrieve_NotificationTemplate_InMemory()
    {
        var options = new DbContextOptionsBuilder<NotificationAuditDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new NotificationAuditDbContext(options);

        var template = NotificationTemplate.Create(
            templateCode: "RESERVATION_CONFIRMATION",
            channel: NotificationChannel.IN_APP,
            bodyContent: "Your reservation {{reservation_id}} is confirmed.",
            subject: "Reservation Confirmed");

        await context.NotificationTemplates.AddAsync(template);
        await context.SaveChangesAsync();

        var retrieved = await context.NotificationTemplates
            .FirstOrDefaultAsync(t => t.TemplateCode == "RESERVATION_CONFIRMATION");

        Assert.NotNull(retrieved);
        Assert.Equal("Reservation Confirmed", retrieved.Subject);
        Assert.Equal(NotificationChannel.IN_APP, retrieved.Channel);
    }

    [Fact]
    public async Task CanAddAndRetrieve_DurableNotification_InMemory()
    {
        var options = new DbContextOptionsBuilder<NotificationAuditDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new NotificationAuditDbContext(options);

        var recipientId = Guid.NewGuid();
        var notification = DurableNotification.Create(
            originatingEvent: "ReservationCreated",
            idempotencyKey: "RES-12345",
            recipientId: recipientId,
            channel: NotificationChannel.IN_APP,
            templateCode: "RESERVATION_CONFIRMATION",
            payload: "{\"slot\": \"A-08\"}");

        await context.DurableNotifications.AddAsync(notification);
        await context.SaveChangesAsync();

        var retrieved = await context.DurableNotifications
            .FirstOrDefaultAsync(n => n.IdempotencyKey == "RES-12345");

        Assert.NotNull(retrieved);
        Assert.Equal(NotificationStatus.PENDING, retrieved.Status);
        Assert.Equal(recipientId, retrieved.RecipientId);
    }

    [Fact]
    public async Task CanAddAndRetrieve_CentralAuditProjection_InMemory()
    {
        var options = new DbContextOptionsBuilder<NotificationAuditDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new NotificationAuditDbContext(options);

        var eventId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();

        var audit = CentralAuditProjection.Create(
            originatingService: "ReservationService",
            eventId: eventId,
            actorId: actorId,
            actorRole: "Driver",
            action: "ReservationCreated",
            resourceType: "Reservation",
            resourceId: resourceId,
            details: "{\"status\": \"CREATED\"}",
            occurredAt: DateTimeOffset.UtcNow);

        await context.CentralAuditProjections.AddAsync(audit);
        await context.SaveChangesAsync();

        var retrieved = await context.CentralAuditProjections
            .FirstOrDefaultAsync(a => a.EventId == eventId);

        Assert.NotNull(retrieved);
        Assert.Equal("ReservationService", retrieved.OriginatingService);
        Assert.Equal("Driver", retrieved.ActorRole);
    }
}
