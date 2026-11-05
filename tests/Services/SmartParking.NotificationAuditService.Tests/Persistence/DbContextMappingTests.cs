using Microsoft.EntityFrameworkCore;
using SmartParking.NotificationAuditService.Domain.Entities;
using SmartParking.NotificationAuditService.Domain.Enums;
using SmartParking.NotificationAuditService.Persistence.Context;
using SmartParking.NotificationAuditService.Persistence.Repositories;

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

    [Fact]
    public void DurableNotificationConfiguration_ShouldMapTitleMessageIsReadReadAtAndUnreadIndex()
    {
        var options = new DbContextOptionsBuilder<NotificationAuditDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new NotificationAuditDbContext(options);
        var entityType = context.Model.FindEntityType(typeof(DurableNotification));
        Assert.NotNull(entityType);

        var titleProp = entityType.FindProperty(nameof(DurableNotification.Title));
        Assert.NotNull(titleProp);
        Assert.Equal("title", titleProp.GetColumnName());
        Assert.Equal(255, titleProp.GetMaxLength());
        Assert.False(titleProp.IsNullable);

        var messageProp = entityType.FindProperty(nameof(DurableNotification.Message));
        Assert.NotNull(messageProp);
        Assert.Equal("message", messageProp.GetColumnName());
        Assert.False(messageProp.IsNullable);

        var isReadProp = entityType.FindProperty(nameof(DurableNotification.IsRead));
        Assert.NotNull(isReadProp);
        Assert.Equal("is_read", isReadProp.GetColumnName());
        Assert.False(isReadProp.IsNullable);
        Assert.Equal(false, isReadProp.GetDefaultValue());

        var readAtProp = entityType.FindProperty(nameof(DurableNotification.ReadAt));
        Assert.NotNull(readAtProp);
        Assert.Equal("read_at", readAtProp.GetColumnName());
        Assert.True(readAtProp.IsNullable);

        var unreadIndex = entityType.GetIndexes()
            .FirstOrDefault(idx => idx.GetDatabaseName() == "idx_durable_notifications_unread");
        Assert.NotNull(unreadIndex);
        var indexPropNames = unreadIndex.Properties.Select(p => p.Name).ToList();
        Assert.Equal(new[] { nameof(DurableNotification.RecipientId), nameof(DurableNotification.IsRead), nameof(DurableNotification.CreatedAt) }, indexPropNames);
    }

    [Fact]
    public async Task NotificationRepository_MarkAllAsReadAsync_ShouldMarkOnlyUnreadNotificationsForSpecificRecipient()
    {
        var options = new DbContextOptionsBuilder<NotificationAuditDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new NotificationAuditDbContext(options);
        var repository = new NotificationRepository(context);

        var recipientA = Guid.NewGuid();
        var recipientB = Guid.NewGuid();

        var notifA1 = DurableNotification.Create("Event1", "Key-A1", recipientA, NotificationChannel.IN_APP, "TPL", "{}", title: "A1", message: "Msg A1");
        var notifA2 = DurableNotification.Create("Event2", "Key-A2", recipientA, NotificationChannel.IN_APP, "TPL", "{}", title: "A2", message: "Msg A2");
        var notifA3 = DurableNotification.Create("Event3", "Key-A3", recipientA, NotificationChannel.IN_APP, "TPL", "{}", title: "A3", message: "Msg A3");
        notifA3.MarkAsRead();

        var notifB1 = DurableNotification.Create("Event4", "Key-B1", recipientB, NotificationChannel.IN_APP, "TPL", "{}", title: "B1", message: "Msg B1");

        await context.DurableNotifications.AddRangeAsync(notifA1, notifA2, notifA3, notifB1);
        await context.SaveChangesAsync();

        var markedCount = await repository.MarkAllAsReadAsync(recipientA);

        Assert.Equal(2, markedCount);

        var updatedA1 = await context.DurableNotifications.FindAsync(notifA1.Id);
        var updatedA2 = await context.DurableNotifications.FindAsync(notifA2.Id);
        var updatedA3 = await context.DurableNotifications.FindAsync(notifA3.Id);
        var updatedB1 = await context.DurableNotifications.FindAsync(notifB1.Id);

        Assert.NotNull(updatedA1);
        Assert.True(updatedA1.IsRead);
        Assert.NotNull(updatedA1.ReadAt);

        Assert.NotNull(updatedA2);
        Assert.True(updatedA2.IsRead);
        Assert.NotNull(updatedA2.ReadAt);

        Assert.NotNull(updatedA3);
        Assert.True(updatedA3.IsRead);

        Assert.NotNull(updatedB1);
        Assert.False(updatedB1.IsRead);
        Assert.Null(updatedB1.ReadAt);
    }

    [Fact]
    public async Task NotificationRepository_MarkAllAsReadAsync_WhenNoUnread_ShouldReturnZero()
    {
        var options = new DbContextOptionsBuilder<NotificationAuditDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new NotificationAuditDbContext(options);
        var repository = new NotificationRepository(context);

        var markedCount = await repository.MarkAllAsReadAsync(Guid.NewGuid());

        Assert.Equal(0, markedCount);
    }

    [Fact]
    public async Task FakeNotificationRepository_MarkAllAsReadAsync_ShouldWorkConsistently()
    {
        var fakeRepo = new TestDoubles.FakeNotificationRepository();
        var recipientId = Guid.NewGuid();

        var notif1 = DurableNotification.Create("Evt1", "K1", recipientId, NotificationChannel.IN_APP, "TPL", "{}");
        var notif2 = DurableNotification.Create("Evt2", "K2", recipientId, NotificationChannel.IN_APP, "TPL", "{}");
        var other = DurableNotification.Create("Evt3", "K3", Guid.NewGuid(), NotificationChannel.IN_APP, "TPL", "{}");

        await fakeRepo.AddAsync(notif1);
        await fakeRepo.AddAsync(notif2);
        await fakeRepo.AddAsync(other);

        var marked = await fakeRepo.MarkAllAsReadAsync(recipientId);

        Assert.Equal(2, marked);
        Assert.True(notif1.IsRead);
        Assert.True(notif2.IsRead);
        Assert.False(other.IsRead);
    }
}
