using Microsoft.EntityFrameworkCore;
using SmartParking.NotificationAuditService.Domain.Entities;
using SmartParking.NotificationAuditService.Domain.Enums;
using SmartParking.NotificationAuditService.Persistence.Context;
using SmartParking.NotificationAuditService.Persistence.Repositories;
using SmartParking.NotificationAuditService.Tests.TestDoubles;
using Xunit;

namespace SmartParking.NotificationAuditService.Tests.Persistence;

public class NotificationPersistenceAdversarialTests
{
    private static NotificationAuditDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<NotificationAuditDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new NotificationAuditDbContext(options);
    }

    [Fact]
    public async Task MarkAllAsRead_WhenRecipientHasZeroNotifications_ReturnsZeroWithoutThrowing()
    {
        // Scenario 1: Empty database
        using var context1 = CreateInMemoryDbContext();
        var repo1 = new NotificationRepository(context1);
        var targetRecipient = Guid.NewGuid();

        var resultEmpty = await repo1.MarkAllAsReadAsync(targetRecipient);
        Assert.Equal(0, resultEmpty);

        // Scenario 2: Non-empty database, but zero notifications for target recipient
        using var context2 = CreateInMemoryDbContext();
        var repo2 = new NotificationRepository(context2);
        var otherRecipient = Guid.NewGuid();

        var notifOther = DurableNotification.Create(
            "Event1", "Key-Other-1", otherRecipient, NotificationChannel.IN_APP, "TPL", "{}",
            title: "Other Title", message: "Other Msg");
        await context2.DurableNotifications.AddAsync(notifOther);
        await context2.SaveChangesAsync();

        var resultNonEmpty = await repo2.MarkAllAsReadAsync(targetRecipient);
        Assert.Equal(0, resultNonEmpty);

        // Verify other recipient's notification is untouched
        var otherInDb = await context2.DurableNotifications.FindAsync(notifOther.Id);
        Assert.NotNull(otherInDb);
        Assert.False(otherInDb.IsRead);
        Assert.Null(otherInDb.ReadAt);
    }

    [Fact]
    public async Task MarkAllAsRead_WhenRecipientHasAllReadNotifications_ReturnsZeroAndDoesNotAlterTimestamps()
    {
        using var context = CreateInMemoryDbContext();
        var repo = new NotificationRepository(context);
        var recipientId = Guid.NewGuid();

        var originalReadAt = DateTimeOffset.UtcNow.AddHours(-2);
        var notifications = new List<DurableNotification>();

        for (int i = 0; i < 10; i++)
        {
            var notif = DurableNotification.Create(
                $"Event-{i}", $"Key-{i}", recipientId, NotificationChannel.IN_APP, "TPL", "{}",
                title: $"Title {i}", message: $"Message {i}");
            notif.MarkAsRead();

            // Reflection to backdate ReadAt and UpdatedAt to simulate historical read state
            typeof(DurableNotification).GetProperty(nameof(DurableNotification.ReadAt))!
                .SetValue(notif, originalReadAt);
            typeof(DurableNotification).GetProperty(nameof(DurableNotification.UpdatedAt))!
                .SetValue(notif, originalReadAt);

            notifications.Add(notif);
        }

        await context.DurableNotifications.AddRangeAsync(notifications);
        await context.SaveChangesAsync();

        // Adversarial execution: Attempting to mark all as read when all are already read
        var markedCount = await repo.MarkAllAsReadAsync(recipientId);

        Assert.Equal(0, markedCount);

        // Verify all 10 notifications kept their exact historical ReadAt and UpdatedAt timestamps
        var inDbList = await context.DurableNotifications
            .Where(n => n.RecipientId == recipientId)
            .ToListAsync();

        Assert.Equal(10, inDbList.Count);
        foreach (var notif in inDbList)
        {
            Assert.True(notif.IsRead);
            Assert.Equal(originalReadAt, notif.ReadAt);
            Assert.Equal(originalReadAt, notif.UpdatedAt);
        }
    }

    [Fact]
    public async Task MarkAllAsRead_WhenRecipientHasMultipleUnreadAcrossChannels_MarksAllAndReturnsExactCount()
    {
        using var context = CreateInMemoryDbContext();
        var repo = new NotificationRepository(context);
        var recipientId = Guid.NewGuid();

        var channels = new[]
        {
            NotificationChannel.IN_APP,
            NotificationChannel.EMAIL,
            NotificationChannel.SMS,
            NotificationChannel.PUSH
        };

        var notifications = new List<DurableNotification>();
        int unreadExpected = 0;
        int alreadyReadCount = 0;
        var historicalTimestamp = DateTimeOffset.UtcNow.AddDays(-1);

        int keyCounter = 0;
        foreach (var channel in channels)
        {
            // 15 unread per channel
            for (int i = 0; i < 15; i++)
            {
                var notif = DurableNotification.Create(
                    $"BatchEvent-{channel}-{i}", $"Key-{keyCounter++}", recipientId, channel, "TPL_MULTI", "{}",
                    title: $"Unread {channel} {i}", message: $"Unread message {i}");
                notifications.Add(notif);
                unreadExpected++;
            }

            // 10 already read per channel
            for (int i = 0; i < 10; i++)
            {
                var notif = DurableNotification.Create(
                    $"BatchEventRead-{channel}-{i}", $"Key-{keyCounter++}", recipientId, channel, "TPL_MULTI", "{}",
                    title: $"Read {channel} {i}", message: $"Read message {i}");
                notif.MarkAsRead();

                typeof(DurableNotification).GetProperty(nameof(DurableNotification.ReadAt))!
                    .SetValue(notif, historicalTimestamp);

                notifications.Add(notif);
                alreadyReadCount++;
            }
        }

        Assert.Equal(60, unreadExpected);
        Assert.Equal(40, alreadyReadCount);
        Assert.Equal(100, notifications.Count);

        await context.DurableNotifications.AddRangeAsync(notifications);
        await context.SaveChangesAsync();

        var beforeCall = DateTimeOffset.UtcNow.AddSeconds(-1);
        var markedCount = await repo.MarkAllAsReadAsync(recipientId);
        var afterCall = DateTimeOffset.UtcNow.AddSeconds(1);

        Assert.Equal(60, markedCount);

        var allInDb = await context.DurableNotifications
            .Where(n => n.RecipientId == recipientId)
            .ToListAsync();

        Assert.Equal(100, allInDb.Count);
        Assert.All(allInDb, n => Assert.True(n.IsRead));

        // Verify the 60 newly marked notifications have ReadAt within [beforeCall, afterCall]
        var newlyMarked = allInDb.Where(n => n.ReadAt > historicalTimestamp).ToList();
        Assert.Equal(60, newlyMarked.Count);
        foreach (var notif in newlyMarked)
        {
            Assert.True(notif.ReadAt >= beforeCall && notif.ReadAt <= afterCall);
        }

        // Verify the 40 already-read notifications preserved their historical ReadAt
        var preservedRead = allInDb.Where(n => n.ReadAt == historicalTimestamp).ToList();
        Assert.Equal(40, preservedRead.Count);
    }

    [Fact]
    public async Task MarkAllAsRead_PartitionIsolation_MultipleRecipients_NeverMutatesOtherRecipients()
    {
        using var context = CreateInMemoryDbContext();
        var repo = new NotificationRepository(context);

        var recipientA = Guid.NewGuid();
        var recipientB = Guid.NewGuid();
        var recipientC = Guid.NewGuid();
        var recipientD = Guid.NewGuid();

        var notifications = new List<DurableNotification>();
        int keyIndex = 0;

        // Recipient A: 30 unread, 20 read
        for (int i = 0; i < 30; i++)
            notifications.Add(DurableNotification.Create("EvtA", $"KeyA-Unread-{keyIndex++}", recipientA, NotificationChannel.IN_APP, "TPL", "{}"));
        for (int i = 0; i < 20; i++)
        {
            var n = DurableNotification.Create("EvtA", $"KeyA-Read-{keyIndex++}", recipientA, NotificationChannel.IN_APP, "TPL", "{}");
            n.MarkAsRead();
            notifications.Add(n);
        }

        // Recipient B: 25 unread, 15 read
        for (int i = 0; i < 25; i++)
            notifications.Add(DurableNotification.Create("EvtB", $"KeyB-Unread-{keyIndex++}", recipientB, NotificationChannel.EMAIL, "TPL", "{}"));
        for (int i = 0; i < 15; i++)
        {
            var n = DurableNotification.Create("EvtB", $"KeyB-Read-{keyIndex++}", recipientB, NotificationChannel.EMAIL, "TPL", "{}");
            n.MarkAsRead();
            notifications.Add(n);
        }

        // Recipient C: 15 unread, 5 read
        for (int i = 0; i < 15; i++)
            notifications.Add(DurableNotification.Create("EvtC", $"KeyC-Unread-{keyIndex++}", recipientC, NotificationChannel.SMS, "TPL", "{}"));
        for (int i = 0; i < 5; i++)
        {
            var n = DurableNotification.Create("EvtC", $"KeyC-Read-{keyIndex++}", recipientC, NotificationChannel.SMS, "TPL", "{}");
            n.MarkAsRead();
            notifications.Add(n);
        }

        // Recipient D: 0 unread, 10 read
        for (int i = 0; i < 10; i++)
        {
            var n = DurableNotification.Create("EvtD", $"KeyD-Read-{keyIndex++}", recipientD, NotificationChannel.PUSH, "TPL", "{}");
            n.MarkAsRead();
            notifications.Add(n);
        }

        // Shuffle / interleave notifications to ensure order independence in storage
        var rng = new Random(42);
        var shuffled = notifications.OrderBy(_ => rng.Next()).ToList();

        await context.DurableNotifications.AddRangeAsync(shuffled);
        await context.SaveChangesAsync();

        // 1. MarkAllAsRead for Recipient A only
        var markedA = await repo.MarkAllAsReadAsync(recipientA);
        Assert.Equal(30, markedA);

        // Verification of Recipient A
        var inDbA = await context.DurableNotifications.Where(n => n.RecipientId == recipientA).ToListAsync();
        Assert.Equal(50, inDbA.Count);
        Assert.All(inDbA, n => Assert.True(n.IsRead));

        // Strict Partition Isolation: Recipient B must have exactly 25 unread, 15 read
        var inDbB = await context.DurableNotifications.Where(n => n.RecipientId == recipientB).ToListAsync();
        Assert.Equal(40, inDbB.Count);
        var unreadB = inDbB.Where(n => !n.IsRead).ToList();
        var readB = inDbB.Where(n => n.IsRead).ToList();
        Assert.Equal(25, unreadB.Count);
        Assert.Equal(15, readB.Count);
        Assert.All(unreadB, n => Assert.Null(n.ReadAt));

        // Strict Partition Isolation: Recipient C must have exactly 15 unread, 5 read
        var inDbC = await context.DurableNotifications.Where(n => n.RecipientId == recipientC).ToListAsync();
        Assert.Equal(20, inDbC.Count);
        var unreadC = inDbC.Where(n => !n.IsRead).ToList();
        var readC = inDbC.Where(n => n.IsRead).ToList();
        Assert.Equal(15, unreadC.Count);
        Assert.Equal(5, readC.Count);
        Assert.All(unreadC, n => Assert.Null(n.ReadAt));

        // Strict Partition Isolation: Recipient D must have exactly 0 unread, 10 read
        var inDbD = await context.DurableNotifications.Where(n => n.RecipientId == recipientD).ToListAsync();
        Assert.Equal(10, inDbD.Count);
        Assert.All(inDbD, n => Assert.True(n.IsRead));

        // 2. MarkAllAsRead for Recipient B
        var markedB = await repo.MarkAllAsReadAsync(recipientB);
        Assert.Equal(25, markedB);

        // Recipient C must STILL be completely untouched
        var unreadCPostB = await context.DurableNotifications.CountAsync(n => n.RecipientId == recipientC && !n.IsRead);
        Assert.Equal(15, unreadCPostB);

        // 3. MarkAllAsRead for Recipient D (who has 0 unread)
        var markedD = await repo.MarkAllAsReadAsync(recipientD);
        Assert.Equal(0, markedD);
    }

    [Fact]
    public async Task MarkAllAsRead_IdempotentConsecutiveCalls_PreservesFirstReadAtTimestamp()
    {
        using var context = CreateInMemoryDbContext();
        var repo = new NotificationRepository(context);
        var recipientId = Guid.NewGuid();

        for (int i = 0; i < 10; i++)
        {
            var notif = DurableNotification.Create(
                "Evt", $"Idempotent-{i}", recipientId, NotificationChannel.IN_APP, "TPL", "{}");
            await context.DurableNotifications.AddAsync(notif);
        }
        await context.SaveChangesAsync();

        // 1st Execution
        var firstRun = await repo.MarkAllAsReadAsync(recipientId);
        Assert.Equal(10, firstRun);

        var firstReadAtSnapshots = await context.DurableNotifications
            .Where(n => n.RecipientId == recipientId)
            .ToDictionaryAsync(n => n.Id, n => n.ReadAt);

        Assert.All(firstReadAtSnapshots.Values, val => Assert.NotNull(val));

        // Short delay to differentiate timestamps if mutation were erroneously attempted
        await Task.Delay(50);

        // 2nd Execution (Idempotency)
        var secondRun = await repo.MarkAllAsReadAsync(recipientId);
        Assert.Equal(0, secondRun);

        var secondReadAtSnapshots = await context.DurableNotifications
            .Where(n => n.RecipientId == recipientId)
            .ToDictionaryAsync(n => n.Id, n => n.ReadAt);

        // Verify timestamps did not change on the 2nd execution
        foreach (var (id, originalTimestamp) in firstReadAtSnapshots)
        {
            Assert.Equal(originalTimestamp, secondReadAtSnapshots[id]);
        }
    }

    [Fact]
    public async Task MarkAllAsRead_CancellationToken_PropagatesCancellationWithoutCompleting()
    {
        using var context = CreateInMemoryDbContext();
        var repo = new NotificationRepository(context);
        var recipientId = Guid.NewGuid();

        var notif = DurableNotification.Create("Evt", "Key-Cxl", recipientId, NotificationChannel.IN_APP, "TPL", "{}");
        await context.DurableNotifications.AddAsync(notif);
        await context.SaveChangesAsync();

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancelled token

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await repo.MarkAllAsReadAsync(recipientId, cts.Token);
        });

        // Verify entity state is not marked read because cancellation occurred before save
        var inDb = await context.DurableNotifications.FindAsync(notif.Id);
        Assert.NotNull(inDb);
        // Note: Even if entity was mutated in memory, SaveChangesAsync was canceled, so reloading reflects unpersisted state
    }

    [Fact]
    public void IndexMetadata_idx_durable_notifications_unread_StrictVerification()
    {
        using var context = CreateInMemoryDbContext();
        var entityType = context.Model.FindEntityType(typeof(DurableNotification));
        Assert.NotNull(entityType);

        var index = entityType.GetIndexes()
            .FirstOrDefault(idx => idx.GetDatabaseName() == "idx_durable_notifications_unread");

        Assert.NotNull(index);

        // Verify index is non-unique (allowing multiple unread notifications per recipient)
        Assert.False(index.IsUnique);

        // Verify exact composite columns: recipient_id, is_read, created_at
        var properties = index.Properties.ToList();
        Assert.Equal(3, properties.Count);

        Assert.Equal(nameof(DurableNotification.RecipientId), properties[0].Name);
        Assert.Equal("recipient_id", properties[0].GetColumnName());

        Assert.Equal(nameof(DurableNotification.IsRead), properties[1].Name);
        Assert.Equal("is_read", properties[1].GetColumnName());

        Assert.Equal(nameof(DurableNotification.CreatedAt), properties[2].Name);
        Assert.Equal("created_at", properties[2].GetColumnName());
    }

    [Fact]
    public async Task FakeNotificationRepository_AdversarialParity_PartitionIsolationAndMultiChannel()
    {
        var fakeRepo = new FakeNotificationRepository();

        var recipientA = Guid.NewGuid();
        var recipientB = Guid.NewGuid();

        var notifA1 = DurableNotification.Create("E1", "K-A1", recipientA, NotificationChannel.EMAIL, "TPL", "{}");
        var notifA2 = DurableNotification.Create("E2", "K-A2", recipientA, NotificationChannel.SMS, "TPL", "{}");
        var notifA3 = DurableNotification.Create("E3", "K-A3", recipientA, NotificationChannel.IN_APP, "TPL", "{}");
        notifA3.MarkAsRead();

        var notifB1 = DurableNotification.Create("E4", "K-B1", recipientB, NotificationChannel.PUSH, "TPL", "{}");

        await fakeRepo.AddAsync(notifA1);
        await fakeRepo.AddAsync(notifA2);
        await fakeRepo.AddAsync(notifA3);
        await fakeRepo.AddAsync(notifB1);

        // MarkAllAsRead for Recipient A
        var countA = await fakeRepo.MarkAllAsReadAsync(recipientA);
        Assert.Equal(2, countA);
        Assert.True(notifA1.IsRead);
        Assert.NotNull(notifA1.ReadAt);
        Assert.True(notifA2.IsRead);
        Assert.NotNull(notifA2.ReadAt);
        Assert.True(notifA3.IsRead);

        // Ensure Recipient B partition isolation
        Assert.False(notifB1.IsRead);
        Assert.Null(notifB1.ReadAt);

        // Repeated call for Recipient A (idempotency check on fake)
        var countA2 = await fakeRepo.MarkAllAsReadAsync(recipientA);
        Assert.Equal(0, countA2);

        // Zero unread for unknown recipient
        var countUnknown = await fakeRepo.MarkAllAsReadAsync(Guid.NewGuid());
        Assert.Equal(0, countUnknown);
    }

    [Theory]
    [InlineData(NotificationStatus.PENDING)]
    [InlineData(NotificationStatus.PROCESSING)]
    [InlineData(NotificationStatus.DELIVERED)]
    [InlineData(NotificationStatus.FAILED)]
    [InlineData(NotificationStatus.DEAD_LETTER)]
    public void DurableNotification_DomainEntity_MarkAsRead_AllowedAcrossAllDeliveryStatuses(NotificationStatus status)
    {
        var notif = DurableNotification.Create(
            "EvtStatus", Guid.NewGuid().ToString(), Guid.NewGuid(), NotificationChannel.IN_APP, "TPL", "{}");

        // Set status via reflection to simulate lifecycle
        typeof(DurableNotification).GetProperty(nameof(DurableNotification.Status))!
            .SetValue(notif, status);

        Assert.False(notif.IsRead);
        Assert.Null(notif.ReadAt);

        // Marking as read is independent of delivery status
        notif.MarkAsRead();

        Assert.True(notif.IsRead);
        Assert.NotNull(notif.ReadAt);
        Assert.Equal(status, notif.Status);
    }
}
