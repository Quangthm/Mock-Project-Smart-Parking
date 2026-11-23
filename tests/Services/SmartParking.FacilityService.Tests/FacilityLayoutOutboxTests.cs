using System.Text.Json;
using System.Text.RegularExpressions;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using RabbitMQ.Client;
using SmartParking.Contracts.Facility;
using SmartParking.FacilityService.Application;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Common.Interfaces.Services;
using SmartParking.FacilityService.Application.Usecases.Gate.Commands.ClaimGateSlot;
using SmartParking.FacilityService.Application.Usecases.Gate.Commands.ReleaseGateSlot;
using SmartParking.FacilityService.Domain;
using SmartParking.FacilityService.Infrastructure.Messaging;
using SmartParking.FacilityService.Persistence;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Infrastructure.Messaging;
using SmartParking.OperationService.Infrastructure.Reservations;
using SmartParking.FacilityService.Tests.TestDoubles;
using OpsOutboxMessage = SmartParking.OperationService.Application.Common.Interfaces.Services.OutboxMessage;

namespace SmartParking.FacilityService.Tests;

public sealed partial class FacilityLayoutOutboxTests
{
    private static readonly HashSet<string> ProtectedDatabases = new(StringComparer.OrdinalIgnoreCase)
    {
        "smartpark_facility",
        "smartpark_operations",
        "smartpark_user",
        "smartpark_payment",
        "smartpark_notification",
        "postgres",
        "template0",
        "template1"
    };

    [GeneratedRegex("^facility_outbox_test_[0-9a-f]{32}$", RegexOptions.CultureInvariant)]
    private static partial Regex IsolatedDbNamePattern();

    [Fact]
    public void ParkingDbContextModel_IncludesMassTransitOutboxEntitiesAndDerivedScript()
    {
        var options = new DbContextOptionsBuilder<ParkingDbContext>()
            .UseNpgsql("Host=localhost;Database=facility_outbox_model_test")
            .Options;
        using var context = new ParkingDbContext(options);

        var inbox = context.Model.FindEntityType(typeof(InboxState));
        var outbox = context.Model.FindEntityType(typeof(OutboxState));
        var message = context.Model.FindEntityType(typeof(MassTransit.EntityFrameworkCoreIntegration.OutboxMessage));

        Assert.NotNull(inbox);
        Assert.NotNull(outbox);
        Assert.NotNull(message);
        Assert.Equal("InboxState", inbox!.GetTableName());
        Assert.Equal("OutboxState", outbox!.GetTableName());
        Assert.Equal("OutboxMessage", message!.GetTableName());
        Assert.Contains(message.GetForeignKeys(), fk => fk.PrincipalEntityType == outbox);
        Assert.Contains(message.GetForeignKeys(), fk => fk.PrincipalEntityType == inbox);

        var script = context.Database.GenerateCreateScript();
        Assert.Contains("CREATE TABLE \"InboxState\"", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE \"OutboxState\"", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE \"OutboxMessage\"", script, StringComparison.Ordinal);

        var sqlPath = Path.Combine(FindRepositoryRoot(), "scripts", "database", "integration", "facility-transactional-outbox.sql");
        Assert.True(File.Exists(sqlPath), $"Missing reviewable SQL file at {sqlPath}");
        var sql = File.ReadAllText(sqlPath);
        Assert.Contains("SET LOCAL lock_timeout = '5s';", sql, StringComparison.Ordinal);
        Assert.Contains("SET LOCAL statement_timeout = '30s';", sql, StringComparison.Ordinal);
        foreach (var expectedToken in new[]
        {
            "\"InboxState\"",
            "\"OutboxState\"",
            "\"OutboxMessage\"",
            "\"PK_InboxState\"",
            "\"AK_InboxState_MessageId_ConsumerId\"",
            "\"PK_OutboxState\"",
            "\"PK_OutboxMessage\"",
            "\"FK_OutboxMessage_InboxState_InboxMessageId_InboxConsumerId\"",
            "\"FK_OutboxMessage_OutboxState_OutboxId\"",
            "\"IX_InboxState_Delivered\"",
            "\"IX_OutboxMessage_EnqueueTime\"",
            "\"IX_OutboxMessage_ExpirationTime\"",
            "\"IX_OutboxMessage_InboxMessageId_InboxConsumerId_SequenceNumber\"",
            "\"IX_OutboxMessage_OutboxId_SequenceNumber\"",
            "\"IX_OutboxState_Created\""
        })
        {
            Assert.Contains(expectedToken, script, StringComparison.Ordinal);
            Assert.Contains(expectedToken, sql, StringComparison.Ordinal);
        }
    }

    [StructurePostgresFact, Trait("Category", "Postgres")]
    public async Task IsolatedPostgres_AppliesFacilityOutboxSql_AndProvesProductionDiBusOutboxAtomicity()
    {
        var admin = Environment.GetEnvironmentVariable("SMARTPARK_STRUCTURE_TEST_CONNECTION")!;
        var dbName = CreateValidatedTestDbName();
        ValidateLocalIsolatedTarget(admin, dbName);

        await using var adminConn = new NpgsqlConnection(admin);
        await adminConn.OpenAsync();
        await new NpgsqlCommand($"CREATE DATABASE {dbName}", adminConn).ExecuteNonQueryAsync();
        var connString = new NpgsqlConnectionStringBuilder(admin) { Database = dbName }.ConnectionString;
        NpgsqlDataSource? dataSource = null;
        try
        {
            dataSource = NpgsqlDataSource.Create(connString);
            var root = FindRepositoryRoot();
            await ExecuteSqlFilesAsync(dataSource, root,
                "scripts/database/microservices/02-facility-service-db.sql",
                "scripts/database/microservices/03a-parking-structure-ops-service-db.sql");

            // Incompatible preexisting object must be aborted by preflight before touching live data.
            var outboxSqlPath = Path.Combine(root, "scripts", "database", "integration", "facility-transactional-outbox.sql");
            var outboxSql = await File.ReadAllTextAsync(outboxSqlPath);
            await using (var conflictConn = await dataSource.OpenConnectionAsync())
            {
                await new NpgsqlCommand("CREATE TABLE \"OutboxMessage\" (\"SequenceNumber\" integer NOT NULL, \"Body\" integer NOT NULL);", conflictConn).ExecuteNonQueryAsync();
                var ex = await Assert.ThrowsAsync<PostgresException>(() => new NpgsqlCommand(outboxSql, conflictConn).ExecuteNonQueryAsync());
                Assert.Contains("Existing outbox tables in Facility DB conflict with MassTransit 8.5.7 model.", ex.MessageText, StringComparison.Ordinal);
                await new NpgsqlCommand("ROLLBACK;", conflictConn).ExecuteNonQueryAsync();
                await new NpgsqlCommand("DROP TABLE \"OutboxMessage\";", conflictConn).ExecuteNonQueryAsync();
            }

            // Apply twice on the same isolated DB to prove idempotence.
            await ExecuteSqlFilesAsync(dataSource, root,
                "scripts/database/integration/facility-transactional-outbox.sql",
                "scripts/database/integration/facility-transactional-outbox.sql");

            var (tenantId, siteId, _, slotId) = await SeedSingleSlotAsync(dataSource);

            await using var provider = BuildFacilityServiceProvider(dataSource, outboxDeliveryEnabled: false, rabbitMqConnection: "amqp://offline:offline@127.0.0.1:1/smartpark");

            // 1. Committed Claim via production DI + IMediator captures MassTransit OutboxState + OutboxMessage atomically.
            using (var scope = provider.CreateScope())
            {
                var publisher = scope.ServiceProvider.GetRequiredService<ILayoutInvalidationPublisher>();
                Assert.IsType<MassTransitLayoutInvalidationPublisher>(publisher);
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var claimed = await mediator.Send(new ClaimGateSlotCommand(tenantId, siteId, "CAR"), CancellationToken.None);
                Assert.Equal(slotId, claimed.Id);
            }

            Guid claimEventId;
            await using (var verifyConn = await dataSource.OpenConnectionAsync())
            {
                var state = (string)(await new NpgsqlCommand($"SELECT physical_state FROM parking_slots WHERE id = '{slotId}'", verifyConn).ExecuteScalarAsync())!;
                Assert.Equal("OCCUPIED", state);

                await using var cmd = new NpgsqlCommand("""
                    SELECT m."OutboxId", m."MessageType", m."Body", o."Delivered"
                    FROM "OutboxMessage" m
                    JOIN "OutboxState" o ON o."OutboxId" = m."OutboxId"
                    """, verifyConn);
                await using var reader = await cmd.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                var outboxId = reader.GetGuid(0);
                var messageType = reader.GetString(1);
                var body = reader.GetString(2);
                var delivered = await reader.IsDBNullAsync(3);
                Assert.NotEqual(Guid.Empty, outboxId);
                Assert.Contains("LayoutInvalidatedV1", messageType, StringComparison.Ordinal);
                Assert.True(delivered, "OutboxState.Delivered must stay NULL while delivery is disabled.");
                claimEventId = AssertValidLayoutInvalidatedBody(body, tenantId, siteId, ["full-layout", "capacity-summary"]);
                Assert.False(await reader.ReadAsync());
            }

            // 2. Rolled-back release leaves parking_slots OCCUPIED and OutboxMessage count at 1.
            using (var rollbackScope = provider.CreateScope())
            {
                var uow = rollbackScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var repo = rollbackScope.ServiceProvider.GetRequiredService<IOperatorGateSlotRepository>();
                var publisher = rollbackScope.ServiceProvider.GetRequiredService<ILayoutInvalidationPublisher>();
                await using var tx = await uow.BeginTransactionAsync();
                var released = await repo.ReleaseAsync(tenantId, siteId, slotId, CancellationToken.None);
                Assert.True(released);
                await publisher.StageAsync(tenantId, siteId, [LayoutInvalidationResources.FullLayout, LayoutInvalidationResources.CapacitySummary]);
                await uow.SaveChangesAsync();
                // Disposed without CommitAsync -> rollback.
            }

            await using (var verifyConn = await dataSource.OpenConnectionAsync())
            {
                var state = (string)(await new NpgsqlCommand($"SELECT physical_state FROM parking_slots WHERE id = '{slotId}'", verifyConn).ExecuteScalarAsync())!;
                var outboxCount = Convert.ToInt64(await new NpgsqlCommand("SELECT count(*) FROM \"OutboxMessage\"", verifyConn).ExecuteScalarAsync());
                Assert.Equal("OCCUPIED", state);
                Assert.Equal(1L, outboxCount);
            }

            // 3. No-op release for a missing/unoccupied slot stages nothing.
            using (var noOpScope = provider.CreateScope())
            {
                var mediator = noOpScope.ServiceProvider.GetRequiredService<IMediator>();
                await mediator.Send(new ReleaseGateSlotCommand(tenantId, siteId, Guid.NewGuid()), CancellationToken.None);
            }

            await using (var verifyConn = await dataSource.OpenConnectionAsync())
            {
                var outboxCount = Convert.ToInt64(await new NpgsqlCommand("SELECT count(*) FROM \"OutboxMessage\"", verifyConn).ExecuteScalarAsync());
                Assert.Equal(1L, outboxCount);
            }

            // 4. Committed release updates parking_slots to AVAILABLE and stages a 2nd distinct outbox message.
            using (var releaseScope = provider.CreateScope())
            {
                var mediator = releaseScope.ServiceProvider.GetRequiredService<IMediator>();
                await mediator.Send(new ReleaseGateSlotCommand(tenantId, siteId, slotId), CancellationToken.None);
            }

            await using (var verifyConn = await dataSource.OpenConnectionAsync())
            {
                var state = (string)(await new NpgsqlCommand($"SELECT physical_state FROM parking_slots WHERE id = '{slotId}'", verifyConn).ExecuteScalarAsync())!;
                var outboxCount = Convert.ToInt64(await new NpgsqlCommand("SELECT count(*) FROM \"OutboxMessage\"", verifyConn).ExecuteScalarAsync());
                var releaseBody = (string)(await new NpgsqlCommand("SELECT \"Body\" FROM \"OutboxMessage\" ORDER BY \"SequenceNumber\" DESC LIMIT 1", verifyConn).ExecuteScalarAsync())!;
                Assert.Equal("AVAILABLE", state);
                Assert.Equal(2L, outboxCount);
                var releaseEventId = AssertValidLayoutInvalidatedBody(releaseBody, tenantId, siteId, ["full-layout", "capacity-summary"]);
                Assert.NotEqual(claimEventId, releaseEventId);
            }
        }
        finally
        {
            await CleanupIsolatedDatabaseAsync(adminConn, dataSource, connString, dbName);
        }
    }

    [StructureRabbitMqFact, Trait("Category", "Postgres"), Trait("Category", "RabbitMQ")]
    public async Task IsolatedPostgresAndRabbitMq_DrainsOutboxViaOperationsRelay_AndRecoversAfterOutage()
    {
        var admin = Environment.GetEnvironmentVariable("SMARTPARK_STRUCTURE_TEST_CONNECTION")!;
        var rabbitConn = Environment.GetEnvironmentVariable("SMARTPARK_RABBITMQ_TEST_CONNECTION")!;
        var dbName = CreateValidatedTestDbName();
        ValidateLocalIsolatedTarget(admin, dbName);

        var testQueueName = "smartpark-operation-layout-invalidated-v1-test-" + Guid.NewGuid().ToString("N");
        await using var adminConn = new NpgsqlConnection(admin);
        await adminConn.OpenAsync();
        await new NpgsqlCommand($"CREATE DATABASE {dbName}", adminConn).ExecuteNonQueryAsync();
        var connString = new NpgsqlConnectionStringBuilder(admin) { Database = dbName }.ConnectionString;
        NpgsqlDataSource? dataSource = null;
        try
        {
            dataSource = NpgsqlDataSource.Create(connString);
            var root = FindRepositoryRoot();
            await ExecuteSqlFilesAsync(dataSource, root,
                "scripts/database/microservices/02-facility-service-db.sql",
                "scripts/database/microservices/03a-parking-structure-ops-service-db.sql",
                "scripts/database/integration/facility-transactional-outbox.sql");

            var (tenantId, siteId, _, slotId) = await SeedSingleSlotAsync(dataSource);
            var broadcasts = new List<OpsOutboxMessage>();
            await using var opsProvider1 = BuildOperationsRelayProvider(rabbitConn, testQueueName, broadcasts);
            await StartHostedServicesAsync(opsProvider1);
            await WaitForBusHealthyAsync(opsProvider1);

            try
            {
                // Stage Claim while delivery is disabled to simulate queued backlog during delivery pause/outage.
                await using (var offlineFacility = BuildFacilityServiceProvider(dataSource, outboxDeliveryEnabled: false, rabbitMqConnection: rabbitConn))
                using (var scope = offlineFacility.CreateScope())
                {
                    await scope.ServiceProvider.GetRequiredService<IMediator>()
                        .Send(new ClaimGateSlotCommand(tenantId, siteId, "CAR"), CancellationToken.None);
                }

                // Start live Facility delivery host; backlog drains via RabbitMQ into Operations LayoutInvalidatedConsumer -> SignalR broadcaster.
                await using (var liveFacility = BuildFacilityServiceProvider(dataSource, outboxDeliveryEnabled: true, rabbitMqConnection: rabbitConn))
                {
                    await StartHostedServicesAsync(liveFacility);
                    try
                    {
                        await WaitUntilAsync(async () =>
                        {
                            var remaining = await CountOutboxMessagesAsync(dataSource);
                            return remaining == 0 && broadcasts.Count >= 1;
                        }, TimeSpan.FromSeconds(15), "Expected Facility outbox message to drain through RabbitMQ to Operations broadcaster.");

                        var first = Assert.Single(broadcasts);
                        Assert.Equal("SnapshotRequired", first.EventType);
                        Assert.Equal(siteId, first.SiteId);
                        Assert.Contains("full-layout", first.Payload, StringComparison.Ordinal);
                        Assert.Contains("capacity-summary", first.Payload, StringComparison.Ordinal);
                    }
                    finally
                    {
                        await StopHostedServicesAsync(liveFacility);
                    }
                }

                // Simulate Operations consumer restart/outage while Facility commits Release and delivers to durable queue.
                await StopHostedServicesAsync(opsProvider1);
            }
            finally
            {
                await StopHostedServicesAsync(opsProvider1);
            }

            await using (var liveFacility2 = BuildFacilityServiceProvider(dataSource, outboxDeliveryEnabled: true, rabbitMqConnection: rabbitConn))
            {
                await StartHostedServicesAsync(liveFacility2);
                try
                {
                    using var scope = liveFacility2.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<IMediator>()
                        .Send(new ReleaseGateSlotCommand(tenantId, siteId, slotId), CancellationToken.None);

                    await WaitUntilAsync(async () => await CountOutboxMessagesAsync(dataSource) == 0,
                        TimeSpan.FromSeconds(15),
                        "Expected Release outbox row to drain to RabbitMQ durable queue while Operations consumer was offline.");
                }
                finally
                {
                    await StopHostedServicesAsync(liveFacility2);
                }
            }

            // Bring Operations relay back online bound to the same durable test queue -> redelivers buffered event.
            var recoveredBroadcasts = new List<OpsOutboxMessage>();
            await using var opsProvider2 = BuildOperationsRelayProvider(rabbitConn, testQueueName, recoveredBroadcasts);
            await StartHostedServicesAsync(opsProvider2);
            try
            {
                await WaitUntilAsync(() => Task.FromResult(recoveredBroadcasts.Count >= 1),
                    TimeSpan.FromSeconds(15),
                    "Expected durable RabbitMQ queue to deliver buffered layout invalidation upon Operations relay restart.");

                var recovered = Assert.Single(recoveredBroadcasts);
                Assert.Equal("SnapshotRequired", recovered.EventType);
                Assert.Equal(siteId, recovered.SiteId);
                Assert.Contains("full-layout", recovered.Payload, StringComparison.Ordinal);
                Assert.Contains("capacity-summary", recovered.Payload, StringComparison.Ordinal);
            }
            finally
            {
                await StopHostedServicesAsync(opsProvider2);
            }
        }
        finally
        {
            await DeleteTestQueueIfExistsAsync(rabbitConn, testQueueName);
            await CleanupIsolatedDatabaseAsync(adminConn, dataSource, connString, dbName);
        }
    }

    private static string CreateValidatedTestDbName() => "facility_outbox_test_" + Guid.NewGuid().ToString("N");

    private static void ValidateLocalIsolatedTarget(string adminConnectionString, string dbName)
    {
        if (!IsolatedDbNamePattern().IsMatch(dbName) || ProtectedDatabases.Contains(dbName))
            throw new InvalidOperationException($"Refusing unsafe test database name '{dbName}'.");

        var builder = new NpgsqlConnectionStringBuilder(adminConnectionString);
        var host = (builder.Host ?? string.Empty).Trim();
        var allowedHosts = new[] { "127.0.0.1", "localhost", "::1" };
        if (!allowedHosts.Contains(host, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Refusing non-local PostgreSQL host '{host}' for isolated tests.");
    }

    private static async Task ExecuteSqlFilesAsync(NpgsqlDataSource dataSource, string root, params string[] relativePaths)
    {
        foreach (var relativePath in relativePaths)
        {
            await using var conn = await dataSource.OpenConnectionAsync();
            var sql = await File.ReadAllTextAsync(Path.Combine(root, relativePath));
            await new NpgsqlCommand(sql, conn).ExecuteNonQueryAsync();
        }
    }

    private static async Task<(Guid TenantId, Guid SiteId, Guid UnitId, Guid SlotId)> SeedSingleSlotAsync(NpgsqlDataSource dataSource)
    {
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        await using var seedConn = await dataSource.OpenConnectionAsync();
        await new NpgsqlCommand($"""
            INSERT INTO tenants(id, code, name, contact_email, contact_phone, status)
            VALUES ('{tenantId}', 'T1', 'Tenant 1', 't1@example.com', '0000000000', 'ACTIVE');
            INSERT INTO parking_sites(id, tenant_id, site_code, name, address, status)
            VALUES ('{siteId}', '{tenantId}', 'S1', 'Site 1', 'Address 1', 'ACTIVE');
            INSERT INTO spatial_units(id, tenant_id, site_id, path, unit_type, name, max_capacity)
            VALUES ('{unitId}', '{tenantId}', '{siteId}', '/A', 'ZONE', 'Zone A', 10);
            INSERT INTO parking_slots(id, tenant_id, site_id, spatial_unit_id, slot_code, supported_vehicle_type, slot_type, physical_state)
            VALUES ('{slotId}', '{tenantId}', '{siteId}', '{unitId}', 'A01', 'CAR', 'STANDARD', 'AVAILABLE');
            """, seedConn).ExecuteNonQueryAsync();
        return (tenantId, siteId, unitId, slotId);
    }

    private static ServiceProvider BuildFacilityServiceProvider(
        NpgsqlDataSource dataSource,
        bool outboxDeliveryEnabled,
        string? rabbitMqConnection)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddDbContext<ParkingDbContext>(options => options.UseNpgsql(dataSource));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IOperatorGateSlotRepository, OperatorGateSlotRepository>();
        services.AddApplicationServices();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Messaging:LayoutOutboxEnabled"] = "true",
                ["Messaging:OutboxDeliveryEnabled"] = outboxDeliveryEnabled ? "true" : "false",
                ["RabbitMq:ConnectionString"] = rabbitMqConnection ?? "amqp://offline:offline@127.0.0.1:1/smartpark"
            })
            .Build();
        services.AddFacilityMessaging(config);
        return services.BuildServiceProvider();
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
        foreach (var service in provider.GetServices<IHostedService>())
            await service.StartAsync(CancellationToken.None);
    }

    private static async Task StopHostedServicesAsync(IServiceProvider provider)
    {
        foreach (var service in provider.GetServices<IHostedService>().Reverse())
        {
            try { await service.StopAsync(CancellationToken.None); }
            catch { /* Best-effort host shutdown during test teardown. */ }
        }
    }

    private static async Task WaitForBusHealthyAsync(IServiceProvider provider)
    {
        var bus = provider.GetRequiredService<IBusControl>();
        await WaitUntilAsync(() => Task.FromResult(bus.CheckHealth().Status == BusHealthStatus.Healthy),
            TimeSpan.FromSeconds(15),
            "Expected MassTransit RabbitMQ bus to become healthy.");
    }

    private static async Task<long> CountOutboxMessagesAsync(NpgsqlDataSource dataSource)
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        return Convert.ToInt64(await new NpgsqlCommand("SELECT count(*) FROM \"OutboxMessage\"", conn).ExecuteScalarAsync());
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout, string failureMessage)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await condition()) return;
            await Task.Delay(150);
        }
        Assert.Fail(failureMessage);
    }

    private static Guid AssertValidLayoutInvalidatedBody(
        string jsonBody,
        Guid expectedTenantId,
        Guid expectedSiteId,
        string[] expectedResources)
    {
        using var doc = JsonDocument.Parse(jsonBody);
        Assert.True(TryGetPropertyIgnoreCase(doc.RootElement, "message", out var messageElement),
            "MassTransit outbox Body must contain 'message' payload.");
        Assert.True(TryGetPropertyIgnoreCase(messageElement, "eventId", out var eventIdEl));
        Assert.True(TryGetPropertyIgnoreCase(messageElement, "schemaVersion", out var versionEl));
        Assert.True(TryGetPropertyIgnoreCase(messageElement, "tenantId", out var tenantEl));
        Assert.True(TryGetPropertyIgnoreCase(messageElement, "siteId", out var siteEl));
        Assert.True(TryGetPropertyIgnoreCase(messageElement, "affectedResources", out var resourcesEl));

        var eventId = eventIdEl.GetGuid();
        Assert.NotEqual(Guid.Empty, eventId);
        Assert.Equal(LayoutInvalidationResources.CurrentSchemaVersion, versionEl.GetInt32());
        Assert.Equal(expectedTenantId, tenantEl.GetGuid());
        Assert.Equal(expectedSiteId, siteEl.GetGuid());
        var actualResources = resourcesEl.EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Equal(expectedResources, actualResources);
        return eventId;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string name, out JsonElement value)
    {
        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    private static async Task DeleteTestQueueIfExistsAsync(string rabbitMqConnection, string queueName)
    {
        if (!queueName.StartsWith("smartpark-operation-layout-invalidated-v1-test-", StringComparison.Ordinal))
            return;
        try
        {
            var factory = new ConnectionFactory { Uri = new Uri(rabbitMqConnection) };
            await using var conn = await factory.CreateConnectionAsync();
            await using var channel = await conn.CreateChannelAsync();
            await channel.QueueDeleteAsync(queueName, ifUnused: false, ifEmpty: false);
            await channel.ExchangeDeleteAsync(queueName, ifUnused: false);
        }
        catch
        {
            // Ignore cleanup failure when RabbitMQ is unavailable during teardown.
        }
    }

    private static async Task CleanupIsolatedDatabaseAsync(
        NpgsqlConnection adminConn,
        NpgsqlDataSource? dataSource,
        string connString,
        string dbName)
    {
        if (dataSource is not null)
            await dataSource.DisposeAsync();
        await using (var poolConn = new NpgsqlConnection(connString))
            NpgsqlConnection.ClearPool(poolConn);
        await new NpgsqlCommand($"DROP DATABASE IF EXISTS {dbName} WITH (FORCE)", adminConn).ExecuteNonQueryAsync();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SmartParking.slnx")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repository root not found.");
    }

    [Fact]
    public async Task ClaimGateSlotCommandHandler_StagesLayoutInvalidationBeforeSaveAndCommit_AndSuppressesOnThrow()
    {
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var slotId = Guid.NewGuid();

        var repo = new FakeGateSlotRepository(new OperatorGateSlot(slotId, "A01", "CAR"), released: true);
        var uow = new FakeUnitOfWork();
        var publisher = new RecordingInvalidationPublisher(uow.Calls);
        repo.Calls = uow.Calls;

        var handler = new ClaimGateSlotCommandHandler(repo, uow, publisher);
        var claimed = await handler.Handle(new ClaimGateSlotCommand(tenantId, siteId, "CAR"), CancellationToken.None);

        Assert.Equal(slotId, claimed.Id);
        Assert.Equal(["begin", "claim", "stage:full-layout,capacity-summary", "save", "commit"], uow.Calls);
        var staged = Assert.Single(publisher.Staged);
        Assert.Equal(tenantId, staged.TenantId);
        Assert.Equal(siteId, staged.SiteId);

        var failingRepo = new FakeGateSlotRepository(null, released: false, throwOnClaim: true);
        var failUow = new FakeUnitOfWork();
        var failPublisher = new RecordingInvalidationPublisher(failUow.Calls);
        failingRepo.Calls = failUow.Calls;

        var failingHandler = new ClaimGateSlotCommandHandler(failingRepo, failUow, failPublisher);
        await Assert.ThrowsAsync<StructureException>(() =>
            failingHandler.Handle(new ClaimGateSlotCommand(tenantId, siteId, "CAR"), CancellationToken.None));
        Assert.Empty(failPublisher.Staged);
        Assert.DoesNotContain("save", failUow.Calls);
        Assert.DoesNotContain("commit", failUow.Calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReleaseGateSlotCommandHandler_StagesOnlyWhenSlotRowChanged(bool rowChanged)
    {
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var slotId = Guid.NewGuid();

        var repo = new FakeGateSlotRepository(new OperatorGateSlot(slotId, "A01", "CAR"), released: rowChanged);
        var uow = new FakeUnitOfWork();
        var publisher = new RecordingInvalidationPublisher(uow.Calls);
        repo.Calls = uow.Calls;

        var handler = new ReleaseGateSlotCommandHandler(repo, uow, publisher);
        await handler.Handle(new ReleaseGateSlotCommand(tenantId, siteId, slotId), CancellationToken.None);

        if (rowChanged)
        {
            Assert.Equal(["begin", "release", "stage:full-layout,capacity-summary", "save", "commit"], uow.Calls);
            Assert.Single(publisher.Staged);
        }
        else
        {
            Assert.Equal(["begin", "release", "save", "commit"], uow.Calls);
            Assert.Empty(publisher.Staged);
        }
    }

    [Fact]
    public async Task MassTransitLayoutInvalidationPublisher_EnforcesWhitelistAndNonEmptyIds()
    {
        var endpoint = new RecordingPublishEndpoint();
        var publisher = new MassTransitLayoutInvalidationPublisher(endpoint);
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            publisher.StageAsync(Guid.Empty, siteId, [LayoutInvalidationResources.FullLayout]));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            publisher.StageAsync(tenantId, Guid.Empty, [LayoutInvalidationResources.FullLayout]));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            publisher.StageAsync(tenantId, siteId, ["bookings", "unknown"]));

        await publisher.StageAsync(
            tenantId,
            siteId,
            [LayoutInvalidationResources.FullLayout, "bookings", LayoutInvalidationResources.CapacitySummary, LayoutInvalidationResources.FullLayout]);

        var published = Assert.Single(endpoint.Published);
        Assert.NotEqual(Guid.Empty, published.EventId);
        Assert.Equal(LayoutInvalidationResources.CurrentSchemaVersion, published.SchemaVersion);
        Assert.Equal(tenantId, published.TenantId);
        Assert.Equal(siteId, published.SiteId);
        Assert.Equal([LayoutInvalidationResources.FullLayout, LayoutInvalidationResources.CapacitySummary], published.AffectedResources);
    }
}
