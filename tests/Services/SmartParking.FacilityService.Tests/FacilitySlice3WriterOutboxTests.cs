using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using RabbitMQ.Client;
using SmartParking.Contracts.Facility;
using SmartParking.FacilityService.Application;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Common.Interfaces.Services;
using SmartParking.FacilityService.Application.Common.Security;
using SmartParking.FacilityService.Application.Reservations;
using SmartParking.FacilityService.Application.Usecases.Occupancy.Commands.ApplyOccupancyProjection;
using SmartParking.FacilityService.Domain;
using SmartParking.FacilityService.Infrastructure;
using SmartParking.FacilityService.Infrastructure.Messaging;
using SmartParking.FacilityService.Persistence;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Infrastructure.Reservations;
using SmartParking.FacilityService.Tests.TestDoubles;
using OpsOutboxMessage = SmartParking.OperationService.Application.Common.Interfaces.Services.OutboxMessage;

namespace SmartParking.FacilityService.Tests;

public sealed class FacilitySlice3WriterOutboxTests
{
    [StructurePostgresFact, Trait("Category", "Postgres")]
    public async Task StructureRepository_CreateCommitRollbackAndNoOp_MatchOutboxContract()
    {
        await using var env = await FacilitySlice3IsolatedDatabaseEnv.CreateAsync();
        await using var provider = env.BuildProvider(outboxDeliveryEnabled: false, rabbitMqConnection: null);

        var owner = new OwnerScope(env.ActorId, env.TenantId);

        // 1. CreateAsync captures structure/full-layout/capacity-summary outbox atomically.
        var createdSite = new SiteProfile(Guid.NewGuid(), env.TenantId, "S3CR", "Slice 3 Site", "1 Test St", null, null, true, 0);
        using (var scope = provider.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IParkingStructureRepository>();
            await repo.CreateAsync(owner, createdSite, CancellationToken.None);
        }

        Assert.Equal(1L, await env.CountOutboxMessagesAsync());
        var createRow = await env.ReadLatestOutboxAsync();
        AssertValidLayoutBody(createRow.Body, env.TenantId, createdSite.Id,
            [LayoutInvalidationResources.Structure, LayoutInvalidationResources.FullLayout, LayoutInvalidationResources.CapacitySummary]);

        // 2. Rollback (BeginEdit + AddUnit disposed without CommitAsync) captures neither unit nor outbox.
        var tokenRollback = await env.PrepareSynchronizationAsync(createdSite.Id);
        using (var scope = provider.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IParkingStructureRepository>();
            await using var edit = await repo.BeginEditAsync(owner, createdSite.Id, new CommitmentSnapshot([], false, []), null, CancellationToken.None);
            edit.Structure.AddUnit(UnitType.ZONE, "Z_ROLLBACK");
        }

        Assert.Equal(1L, await env.CountOutboxMessagesAsync());
        Assert.Equal(0L, await env.ScalarLongAsync($"SELECT count(*) FROM spatial_units WHERE site_id = '{createdSite.Id}'"));

        // 3. Committed edit captures state + outbox atomically.
        var tokenCommit = await env.PrepareSynchronizationAsync(createdSite.Id);
        Guid zoneId;
        using (var scope = provider.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IParkingStructureRepository>();
            await using var edit = await repo.BeginEditAsync(owner, createdSite.Id, new CommitmentSnapshot([], false, []), null, CancellationToken.None);
            zoneId = edit.Structure.AddUnit(UnitType.ZONE, "Z_LIVE", maxCapacity: 5);
            edit.Structure.AddSlot(zoneId, "C01", VehicleType.CAR);
            var outcome = StructureOutcomeBuilder.Build(edit.Structure, new HashSet<Guid>(), new HashSet<Guid>());
            await edit.CommitAsync(tokenCommit, outcome, null, null, CancellationToken.None);
        }

        Assert.Equal(2L, await env.CountOutboxMessagesAsync());
        var editRow = await env.ReadLatestOutboxAsync();
        AssertValidLayoutBody(editRow.Body, env.TenantId, createdSite.Id,
            [LayoutInvalidationResources.Structure, LayoutInvalidationResources.FullLayout, LayoutInvalidationResources.CapacitySummary]);

        // 4. No-op edit (no mutation, before == after) commits ready synchronization with 0 new outbox rows.
        var tokenNoOp = await env.PrepareSynchronizationAsync(createdSite.Id);
        using (var scope = provider.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IParkingStructureRepository>();
            await using var edit = await repo.BeginEditAsync(owner, createdSite.Id, new CommitmentSnapshot([], false, []), null, CancellationToken.None);
            var originalSlots = edit.Structure.Slots.Select(s => s.Id).ToHashSet();
            var originalUnits = edit.Structure.Units.Select(u => u.Id).ToHashSet();
            var outcome = StructureOutcomeBuilder.Build(edit.Structure, originalSlots, originalUnits);
            await edit.CommitAsync(tokenNoOp, outcome, null, null, CancellationToken.None);
        }

        Assert.Equal(2L, await env.CountOutboxMessagesAsync());
        Assert.Equal("ready", await env.ScalarStringAsync($"SELECT state FROM structure_synchronizations WHERE token = '{tokenNoOp}'"));
        _ = tokenRollback;
    }

    [StructurePostgresFact, Trait("Category", "Postgres")]
    public async Task MaintenanceWindowStore_EmitsLayoutAndMaintenanceOutbox_AndRollbackProducesZeroOutboxRows()
    {
        await using var env = await FacilitySlice3IsolatedDatabaseEnv.CreateAsync();
        await using var provider = env.BuildProvider(outboxDeliveryEnabled: false, rabbitMqConnection: null);
        var owner = new OwnerScope(env.ActorId, env.TenantId);
        var start = DateTime.UtcNow.AddHours(1);
        var end = DateTime.UtcNow.AddHours(3);

        MaintenanceWindow window;
        using (var scope = provider.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IMaintenanceWindowStore>();
            window = await store.CreateAsync(owner, env.SiteId, start, end, "Planned resurfacing", CancellationToken.None);
        }

        Assert.Equal(2L, await env.CountOutboxMessagesAsync());

        // Overlapping window fails exclusion constraint and emits 0 new outbox rows.
        using (var scope = provider.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IMaintenanceWindowStore>();
            var ex = await Assert.ThrowsAsync<StructureException>(() =>
                store.CreateAsync(owner, env.SiteId, start.AddMinutes(30), end.AddMinutes(30), "Overlap", CancellationToken.None));
            Assert.Equal("STRUCTURE_IN_USE", ex.Code);
        }
        Assert.Equal(2L, await env.CountOutboxMessagesAsync());

        // Extend emits both layout invalidation and revision=2 maintenance event.
        using (var scope = provider.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IMaintenanceWindowStore>();
            _ = await store.ExtendAsync(owner, env.SiteId, window.Id, end.AddHours(1), CancellationToken.None);
        }
        Assert.Equal(4L, await env.CountOutboxMessagesAsync());

        // Cancel emits both layout invalidation and revision=3 CLEARED maintenance event.
        using (var scope = provider.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IMaintenanceWindowStore>();
            await store.CancelAsync(owner, env.SiteId, window.Id, CancellationToken.None);
        }
        Assert.Equal(6L, await env.CountOutboxMessagesAsync());
    }

    [StructurePostgresFact, Trait("Category", "Postgres")]
    public async Task OccupancyProjectionRepository_EmitsAtomicallyWithProjection_AndSuppressesDuplicatesStaleAndSpentNoOps()
    {
        await using var env = await FacilitySlice3IsolatedDatabaseEnv.CreateAsync();
        await using var provider = env.BuildProvider(outboxDeliveryEnabled: false, rabbitMqConnection: null);

        var occEventId = Guid.NewGuid();
        var sessionA = Guid.NewGuid();
        var relEventId = Guid.NewGuid();

        // 1. Fresh SlotStateChanged (AVAILABLE -> OCCUPIED) emits exact EventId outbox message.
        using (var scope = provider.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            Assert.True(await mediator.Send(new ApplyOccupancyProjectionCommand(
                new OccupancyProjectionEvent(occEventId, "SlotStateChanged", DateTimeOffset.UtcNow, 1, env.SiteId, env.SlotId, sessionA, "OCCUPIED")),
                CancellationToken.None));
        }

        Assert.Equal(1L, await env.CountOutboxMessagesAsync());
        var occOutbox = await env.ReadLatestOutboxAsync();
        Assert.Equal(occEventId, AssertValidLayoutBody(occOutbox.Body, env.TenantId, env.SiteId,
            [LayoutInvalidationResources.FullLayout, LayoutInvalidationResources.CapacitySummary]));

        // 2. Duplicate eventId returns false and emits 0 new outbox rows.
        using (var scope = provider.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            Assert.False(await mediator.Send(new ApplyOccupancyProjectionCommand(
                new OccupancyProjectionEvent(occEventId, "SlotStateChanged", DateTimeOffset.UtcNow, 2, env.SiteId, env.SlotId, sessionA, "AVAILABLE")),
                CancellationToken.None));
        }
        Assert.Equal(1L, await env.CountOutboxMessagesAsync());

        // 3. Same-state no-op (already OCCUPIED) advances watermark without emitting outbox row.
        using (var scope = provider.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            Assert.True(await mediator.Send(new ApplyOccupancyProjectionCommand(
                new OccupancyProjectionEvent(Guid.NewGuid(), "SlotStateChanged", DateTimeOffset.UtcNow, 2, env.SiteId, env.SlotId, sessionA, "OCCUPIED")),
                CancellationToken.None));
        }
        Assert.Equal(1L, await env.CountOutboxMessagesAsync());

        // 4. Stale lower sequence returns false and emits 0 new outbox rows.
        using (var scope = provider.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            Assert.False(await mediator.Send(new ApplyOccupancyProjectionCommand(
                new OccupancyProjectionEvent(Guid.NewGuid(), "SlotStateChanged", DateTimeOffset.UtcNow, 1, env.SiteId, env.SlotId, sessionA, "AVAILABLE")),
                CancellationToken.None));
        }
        Assert.Equal(1L, await env.CountOutboxMessagesAsync());

        // 5. OccupancyReleased (OCCUPIED -> AVAILABLE) emits releaseEventId atomically with release ledger.
        using (var scope = provider.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            Assert.True(await mediator.Send(new ApplyOccupancyProjectionCommand(
                new OccupancyProjectionEvent(relEventId, "OccupancyReleased", DateTimeOffset.UtcNow, 3, env.SiteId, env.SlotId, sessionA, null)),
                CancellationToken.None));
        }
        Assert.Equal(2L, await env.CountOutboxMessagesAsync());
        Assert.Equal(relEventId, AssertValidLayoutBody((await env.ReadLatestOutboxAsync()).Body, env.TenantId, env.SiteId,
            [LayoutInvalidationResources.FullLayout, LayoutInvalidationResources.CapacitySummary]));

        // 6. Re-occupy with session B (seq 4 -> emits 3rd outbox), then redeliver spent release for session A (seq 5 -> 0 new outbox!).
        using (var scope = provider.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            Assert.True(await mediator.Send(new ApplyOccupancyProjectionCommand(
                new OccupancyProjectionEvent(Guid.NewGuid(), "SlotStateChanged", DateTimeOffset.UtcNow, 4, env.SiteId, env.SlotId, Guid.NewGuid(), "OCCUPIED")),
                CancellationToken.None));
            Assert.True(await mediator.Send(new ApplyOccupancyProjectionCommand(
                new OccupancyProjectionEvent(Guid.NewGuid(), "OccupancyReleased", DateTimeOffset.UtcNow, 5, env.SiteId, env.SlotId, sessionA, null)),
                CancellationToken.None));
        }
        Assert.Equal(3L, await env.CountOutboxMessagesAsync());
        Assert.Equal("OCCUPIED", await env.ScalarStringAsync($"SELECT physical_state FROM parking_slots WHERE id = '{env.SlotId}'"));

        // 7. Release with null SlotId records processed_events with 0 outbox rows.
        using (var scope = provider.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            Assert.False(await mediator.Send(new ApplyOccupancyProjectionCommand(
                new OccupancyProjectionEvent(Guid.NewGuid(), "OccupancyReleased", DateTimeOffset.UtcNow, 6, env.SiteId, null, Guid.NewGuid(), null)),
                CancellationToken.None));
        }
        Assert.Equal(3L, await env.CountOutboxMessagesAsync());
    }

    [StructureRabbitMqFact, Trait("Category", "RabbitMQ")]
    public async Task OccupancyProjectionWorker_CommitsAtomicallyBeforeAck_AndRedeliveryProducesNoDuplicateInvalidation()
    {
        await using var env = await FacilitySlice3IsolatedDatabaseEnv.CreateAsync();
        var rabbitConn = Environment.GetEnvironmentVariable("SMARTPARK_RABBITMQ_TEST_CONNECTION")!;
        var suffix = Guid.NewGuid().ToString("N");
        var projExchange = $"smartpark-occupancy-test-{suffix}";
        var projQueue = $"smartpark-facility-occupancy-test-{suffix}";
        var relayQueue = $"smartpark-operation-layout-invalidated-v1-test-{suffix}";

        var broadcasts = new List<OpsOutboxMessage>();
        await using var opsProvider = BuildOperationsRelayProvider(rabbitConn, relayQueue, broadcasts);
        await StartHostedServicesAsync(opsProvider);
        await WaitForBusHealthyAsync(opsProvider);

        await using var facilityProvider = env.BuildProvider(outboxDeliveryEnabled: true, rabbitMqConnection: rabbitConn);
        var worker = new OccupancyProjectionWorker(
            facilityProvider.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(new RabbitMqProjectionOptions
            {
                ConnectionString = rabbitConn,
                Exchange = projExchange,
                Queue = projQueue,
                PrefetchCount = 4
            }),
            NullLogger<OccupancyProjectionWorker>.Instance);

        try
        {
            await StartHostedServicesAsync(facilityProvider);
            await WaitForBusHealthyAsync(facilityProvider);
            await worker.StartAsync(CancellationToken.None);

            var eventId = Guid.NewGuid();
            var payloadJson = JsonSerializer.Serialize(new
            {
                eventId,
                eventType = "SlotStateChanged",
                occurredAt = DateTimeOffset.UtcNow,
                sequenceNumber = 1L,
                siteId = env.SiteId,
                payload = new { slotId = env.SlotId, sessionId = Guid.NewGuid(), physicalState = "OCCUPIED" }
            });

            var factory = new ConnectionFactory { Uri = new Uri(rabbitConn) };
            await using var conn = await factory.CreateConnectionAsync();
            await WaitUntilAsync(async () =>
                {
                    try
                    {
                        await using var probe = await conn.CreateChannelAsync();
                        var ok = await probe.QueueDeclarePassiveAsync(projQueue);
                        return ok.ConsumerCount >= 1;
                    }
                    catch
                    {
                        return false;
                    }
                },
                TimeSpan.FromSeconds(10),
                "Expected OccupancyProjectionWorker to bind queue and start consuming.");
            await using var channel = await conn.CreateChannelAsync();

            await channel.BasicPublishAsync(projExchange, "occupancy.changed", Encoding.UTF8.GetBytes(payloadJson));

            await WaitUntilAsync(
                async () => broadcasts.Count == 1 && await env.CountOutboxMessagesAsync() == 0 &&
                    await env.ScalarStringAsync($"SELECT physical_state FROM parking_slots WHERE id = '{env.SlotId}'") == "OCCUPIED",
                TimeSpan.FromSeconds(15),
                "Expected OccupancyProjectionWorker to commit projection + outbox and drain through RabbitMQ to Operations.");

            Assert.Equal(eventId, broadcasts[0].EventId);

            // Redeliver the exact same eventId -> consumer drops duplicate and ACKs cleanly with 0 extra broadcasts.
            await channel.BasicPublishAsync(projExchange, "occupancy.changed", Encoding.UTF8.GetBytes(payloadJson));
            await Task.Delay(1200);

            Assert.Single(broadcasts);
            Assert.Equal(0L, await env.CountOutboxMessagesAsync());
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
            await StopHostedServicesAsync(facilityProvider);
            await StopHostedServicesAsync(opsProvider);
            await DeleteTestTopologyAsync(rabbitConn, relayQueue, null);
            await DeleteTestTopologyAsync(rabbitConn, projQueue, projExchange);
        }
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout, string message)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await condition()) return;
            await Task.Delay(150);
        }
        Assert.Fail(message);
    }

    private static Guid AssertValidLayoutBody(string jsonBody, Guid expectedTenantId, Guid expectedSiteId, string[] expectedResources)
    {
        using var doc = JsonDocument.Parse(jsonBody);
        var msg = doc.RootElement.GetProperty("message");
        var eventId = msg.GetProperty("eventId").GetGuid();
        Assert.NotEqual(Guid.Empty, eventId);
        Assert.Equal(LayoutInvalidationResources.CurrentSchemaVersion, msg.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(expectedTenantId, msg.GetProperty("tenantId").GetGuid());
        Assert.Equal(expectedSiteId, msg.GetProperty("siteId").GetGuid());
        var actual = msg.GetProperty("affectedResources").EnumerateArray().Select(x => x.GetString()!).ToArray();
        Assert.Equal(expectedResources, actual);
        return eventId;
    }

    private static ServiceProvider BuildOperationsRelayProvider(
        string rabbitMqConnection,
        string queueName,
        List<OpsOutboxMessage> broadcasts)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IRealtimeLayoutBroadcaster>(new ThreadSafeRecordingBroadcaster(broadcasts));
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReservationAdmission:Enabled"] = "false",
                ["Messaging:AuditEnabled"] = "false",
                ["Messaging:Enabled"] = "false",
                ["Messaging:LayoutRelayEnabled"] = "true",
                ["Messaging:LayoutRelayQueueName"] = queueName,
                ["RabbitMq:ConnectionString"] = rabbitMqConnection
            })
            .Build();
        services.AddReservationMessaging(config);
        return services.BuildServiceProvider();
    }

    private static async Task StartHostedServicesAsync(IServiceProvider provider)
    {
        foreach (var svc in provider.GetServices<IHostedService>())
            await svc.StartAsync(CancellationToken.None);
    }

    private static async Task StopHostedServicesAsync(IServiceProvider provider)
    {
        foreach (var svc in provider.GetServices<IHostedService>().Reverse())
        {
            try { await svc.StopAsync(CancellationToken.None); }
            catch { /* teardown cleanup */ }
        }
    }

    private static async Task WaitForBusHealthyAsync(IServiceProvider provider)
    {
        var bus = provider.GetRequiredService<IBusControl>();
        await WaitUntilAsync(
            () => Task.FromResult(bus.CheckHealth().Status == BusHealthStatus.Healthy),
            TimeSpan.FromSeconds(15),
            "Expected MassTransit bus to become healthy.");
    }

    private static async Task DeleteTestTopologyAsync(string rabbitMqConnection, string queueName, string? exchangeName)
    {
        try
        {
            var factory = new ConnectionFactory { Uri = new Uri(rabbitMqConnection) };
            await using var conn = await factory.CreateConnectionAsync();
            await using var channel = await conn.CreateChannelAsync();
            await channel.QueueDeleteAsync(queueName, ifUnused: false, ifEmpty: false);
            await channel.ExchangeDeleteAsync(queueName, ifUnused: false);
            if (!string.IsNullOrWhiteSpace(exchangeName))
                await channel.ExchangeDeleteAsync(exchangeName, ifUnused: false);
        }
        catch
        {
            // Best-effort RabbitMQ test topology cleanup.
        }
    }
}
