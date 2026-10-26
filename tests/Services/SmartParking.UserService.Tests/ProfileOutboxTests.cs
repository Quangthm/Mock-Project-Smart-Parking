using System.Text.Json;
using System.Text.RegularExpressions;
using System.Net;
using System.Net.Sockets;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Interfaces.MessageBroker;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.Profiles.Commands;
using UserService.Application.Profiles.Events;
using UserService.Infrastructure.Messaging;
using UserService.Persistence;
using UserService.Persistence.Repositories;

namespace SmartParking.UserService.Tests;

public sealed class ProfileOutboxTests
{
    [Fact]
    public void ReviewedSqlMatchesExactFrameworkModel()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql().Options);
        var generated = db.Database.GenerateCreateScript();
        var statements = Regex.Matches(generated,
            "CREATE TABLE \"(?:InboxState|OutboxState|OutboxMessage)\"[\\s\\S]*?;|CREATE (?:UNIQUE )?INDEX \"IX_(?:InboxState|OutboxState|OutboxMessage)[\\s\\S]*?;")
            .Select(match => match.Value).ToArray();
        Assert.Equal(9, statements.Length);
        var script = File.ReadAllText(ServiceSchema.FeaturePath("microservices/14-user-profile-outbox.sql"));
        var reviewed = script.Split("-- BEGIN FRAMEWORK DDL")[1].Split("-- END FRAMEWORK DDL")[0];
        Assert.Equal(Normalize(string.Join("\n", statements)), Normalize(reviewed));
    }

    [Fact]
    public async Task CaptureOnlyConfigurationKeepsTheScopedPublisherAndDisablesDeliveryWorker()
    {
        await using var provider = Services("Host=unused;Database=unused").BuildServiceProvider();
        Assert.DoesNotContain(provider.GetServices<IHostedService>(), worker =>
            worker is BusOutboxDeliveryService<AppDbContext>);
        await using var scope = provider.CreateAsyncScope();
        Assert.IsType<MassTransitEventPublisher>(scope.ServiceProvider.GetRequiredService<IEventPublisher>());
    }

    [PostgresFact]
    public async Task CaptureOnlyHostStartsAndRecordsDurablyWithoutAnAvailableBroker()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        var userId = await SeedAsync(fixture);
        // Reserve an isolated port with no AMQP server. The TCP handshake cannot establish
        // a working bus, so successful capture cannot depend on RabbitMQ availability.
        using var unavailableBroker = new TcpListener(IPAddress.Loopback, 0);
        unavailableBroker.Start();
        var port = ((IPEndPoint)unavailableBroker.LocalEndpoint).Port;
        var configuration = Configuration(port, deliver: false);
        using var host = Host.CreateDefaultBuilder().ConfigureLogging(log => log.ClearProviders())
            .ConfigureServices(services =>
            {
                services.AddDbContext<AppDbContext>(options => options.UseNpgsql(fixture.ConnectionString));
                services.AddProfileMessaging(configuration, registration => registration.AddProfileOutbox(configuration));
            }).Build();
        await host.StartAsync().WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            var message = NewEvent(userId);
            await WriteAsync(host.Services, userId, "Captured without broker", message, commit: true)
                .WaitAsync(TimeSpan.FromSeconds(10));
            await using var db = fixture.Open();
            Assert.Equal("Captured without broker", (await db.Users.SingleAsync()).FullName);
            Assert.Equal(message.EventId, (await db.Set<OutboxMessage>().SingleAsync()).MessageId);
            Assert.Single(await db.Set<OutboxState>().ToListAsync());
            Assert.DoesNotContain(host.Services.GetServices<IHostedService>(), worker => worker is BusOutboxDeliveryService<AppDbContext>);
        }
        finally { await host.StopAsync().WaitAsync(TimeSpan.FromSeconds(15)); }
    }

    [PostgresFact]
    public async Task ScopedPublicationCommitsWithNameAndRollsBackWithName()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        var userId = await SeedAsync(fixture);
        await using var provider = Services(fixture.ConnectionString).BuildServiceProvider();
        var committed = NewEvent(userId);
        await WriteAsync(provider, userId, "Committed", committed, commit: true);
        await using (var db = fixture.Open())
        {
            Assert.Equal("Committed", (await db.Users.SingleAsync()).FullName);
            var stored = await db.Set<OutboxMessage>().SingleAsync();
            Assert.Equal(committed.EventId, stored.MessageId);
            Assert.Equal(await db.Set<OutboxState>().Select(state => state.OutboxId).SingleAsync(), stored.OutboxId);
            using var json = JsonDocument.Parse(stored.Body);
            var payload = json.RootElement.GetProperty("message");
            Assert.Equal(new[] { "actorAccountId", "affectedUserId", "correlationId", "eventId", "occurredAtUtc", "schemaVersion" },
                payload.EnumerateObject().Select(p => p.Name).Order().ToArray());
            Assert.Equal(committed.EventId, payload.GetProperty("eventId").GetGuid());
            Assert.DoesNotContain("Committed", stored.Body);
        }
        await WriteAsync(provider, userId, "Rolled back", NewEvent(userId), commit: false);
        await using (var db = fixture.Open())
        {
            Assert.Equal("Committed", (await db.Users.SingleAsync()).FullName);
            Assert.Equal(1, await db.Set<OutboxMessage>().CountAsync());
            Assert.Equal(1, await db.Set<OutboxState>().CountAsync());
        }
    }

    [PostgresFact]
    public async Task ActualProfileHandlerCapturesChangedOnlyAndRejectedWritesCaptureNothing()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        var userId = await SeedAsync(fixture);
        var accountId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        await using (var db = fixture.Open())
        {
            db.Accounts.Add(new Account { Id = accountId, UserId = userId,
                AccountRoles = [new AccountRole { AccountId = accountId, RoleCode = "DRIVER" }] });
            db.AuthSessions.Add(new AuthSession
            {
                Id = Guid.NewGuid(), UserId = userId, AccountId = accountId, AccessTokenId = tokenId,
                TokenHash = "isolated-outbox-session", CreatedAt = DateTimeOffset.UtcNow,
                AccessExpiresAt = DateTimeOffset.UtcNow.AddHours(1), ExpiresAt = DateTimeOffset.UtcNow.AddDays(1)
            });
            await db.SaveChangesAsync();
        }
        await using var provider = Services(fixture.ConnectionString).BuildServiceProvider();
        async Task SaveAsync(string name)
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var actor = new TestCurrentActor(userId, "driver", accountId, tokenId);
            var handler = new UpdateOwnProfileCommandHandler(new UnitOfWork(db, new UserRepository(db)),
                new ProfileAccessStore(db), actor, scope.ServiceProvider.GetRequiredService<IEventPublisher>(), TimeProvider.System);
            await handler.Handle(new UpdateOwnProfileCommand(name), default);
        }
        await SaveAsync("  After  ");
        await SaveAsync("After");
        await using (var db = fixture.Open())
        {
            Assert.Equal("After", (await db.Users.SingleAsync()).FullName);
            var saved = await db.Set<OutboxMessage>().SingleAsync();
            using var payload = JsonDocument.Parse(saved.Body);
            Assert.Equal(accountId, payload.RootElement.GetProperty("message").GetProperty("actorAccountId").GetGuid());
            Assert.Equal(userId, payload.RootElement.GetProperty("message").GetProperty("affectedUserId").GetGuid());
            await db.AuthSessions.ExecuteUpdateAsync(update => update.SetProperty(session => session.IsRevoked, true));
        }
        var rejected = await Assert.ThrowsAsync<AuthException>(() => SaveAsync("Rejected"));
        Assert.Equal("INVALID_TOKEN", rejected.Code);
        await using (var db = fixture.Open())
        {
            Assert.Equal("After", (await db.Users.SingleAsync()).FullName);
            Assert.Equal(1, await db.Set<OutboxMessage>().CountAsync());
        }
    }

    [PostgresFact]
    public async Task DatabaseFailureInEitherWriteLeavesNeitherMutationNorOutboxRecord()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        var userId = await SeedAsync(fixture);
        await using var provider = Services(fixture.ConnectionString).BuildServiceProvider();
        // A real database constraint rejects the business mutation after an event was captured.
        await Assert.ThrowsAsync<DbUpdateException>(() => WriteAsync(provider, userId, new string('x', 256), NewEvent(userId), true));
        await using (var db = fixture.Open())
        {
            Assert.Equal("Before", (await db.Users.SingleAsync()).FullName);
            Assert.Empty(await db.Set<OutboxMessage>().ToListAsync());
            Assert.Empty(await db.Set<OutboxState>().ToListAsync());
            // Simulate a storage fault in the outbox itself on this disposable fixture only.
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"OutboxMessage\" ADD CONSTRAINT test_reject_outbox CHECK (false)");
        }
        await Assert.ThrowsAsync<DbUpdateException>(() => WriteAsync(provider, userId, "Must roll back", NewEvent(userId), true));
        await using (var db = fixture.Open())
        {
            Assert.Equal("Before", (await db.Users.SingleAsync()).FullName);
            Assert.Empty(await db.Set<OutboxMessage>().ToListAsync());
            Assert.Empty(await db.Set<OutboxState>().ToListAsync());
        }
    }

    internal static ServiceCollection Services(string connection, int port = 5672, bool deliver = false)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.ClearProviders());
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connection));
        var configuration = Configuration(port, deliver);
        services.AddProfileMessaging(configuration, registration => registration.AddProfileOutbox(configuration));
        return services;
    }

    internal static IConfiguration Configuration(int port, bool deliver) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Messaging:OutboxDeliveryEnabled"] = deliver.ToString(),
            ["Messaging:RabbitMq:Host"] = "127.0.0.1",
            ["Messaging:RabbitMq:Port"] = port.ToString(),
            ["Messaging:RabbitMq:Username"] = "guest",
            ["Messaging:RabbitMq:Password"] = "guest"
        }).Build();

    internal static async Task<Guid> SeedAsync(UserServiceDatabaseFixture fixture)
    {
        await using var db = fixture.Open();
        var userId = Guid.NewGuid();
        db.Users.Add(new User { Id = userId, FullName = "Before", Email = "outbox@example.test",
            PasswordHash = "unused", Status = UserStatus.Active });
        await db.SaveChangesAsync();
        return userId;
    }

    internal static UserProfileNameChanged NewEvent(Guid userId) =>
        new(Guid.NewGuid(), Guid.NewGuid(), userId, DateTimeOffset.UtcNow, "test-correlation");

    internal static async Task WriteAsync(IServiceProvider provider, Guid userId, string name,
        UserProfileNameChanged message, bool commit)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var user = await db.Users.SingleAsync(u => u.Id == userId);
        user.FullName = name; // Deliberately bypass domain validation to exercise real database failure.
        await scope.ServiceProvider.GetRequiredService<IEventPublisher>().PublishAsync(message, default);
        await db.SaveChangesAsync();
        if (commit) await transaction.CommitAsync();
    }

    private static string Normalize(string sql) => Regex.Replace(sql, "\\s+", " ").Trim();
}
