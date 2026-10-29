using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.MessageBroker;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.Profiles.Commands;
using UserService.Application.Profiles.Events;
using UserService.Persistence;
using UserService.Persistence.Repositories;

namespace SmartParking.UserService.Tests;

public sealed class ProfileAuthorizationRaceTests
{
    [PostgresFact]
    public async Task StaleTrackedAuthorityIsRefreshedAfterCommittedRevocation()
    {
        await using var fixture = await Database.CreateAsync();
        await using var profile = fixture.Context();
        var staleUser = await profile.Users.Include(u => u.Accounts).ThenInclude(a => a.Role)
            .SingleAsync(u => u.Id == fixture.UserId);
        await profile.AuthSessions.SingleAsync(s => s.Id == fixture.SessionId);
        await using (var revoke = fixture.Context())
        {
            Assert.True(await new PostgresAuthSessionStore(revoke, fixture.Clock)
                .RevokeAsync(fixture.TokenId, fixture.UserId, "refresh-hash", default));
            await revoke.Accounts.Where(a => a.Id == fixture.AccountId).ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, "LOCKED"));
            await revoke.Users.Where(u => u.Id == fixture.UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.FullName, "Fresh Name"));
        }
        await using (var tx = await profile.Database.BeginTransactionAsync())
        {
            var state = await new CurrentAccessStore(profile).LockCurrentAsync(fixture.AccountId, fixture.TokenId, default);
            Assert.NotNull(state);
            Assert.Same(staleUser, state.User);
            Assert.Equal("Fresh Name", state.User.FullName);
            Assert.True(state.Session.IsRevoked);
            Assert.Equal("LOCKED", state.Account.Status);
            Assert.Equal("ACTIVE", staleUser.Accounts.Single(a => a.Id == fixture.AccountId).Status);
            Assert.NotSame(staleUser.Accounts.Single(), state.Account);
            await tx.RollbackAsync();
        }
        var events = new CapturedEvents();
        var rejected = await Assert.ThrowsAsync<AuthException>(() => Handler(profile, fixture, events)
            .Handle(new UpdateOwnProfileCommand("Must Not Save"), default));
        Assert.Equal("INVALID_TOKEN", rejected.Code);
        Assert.Empty(events.Messages);
        await using var verify = fixture.Context();
        Assert.Equal("Fresh Name", (await verify.Users.SingleAsync(u => u.Id == fixture.UserId)).FullName);
    }

    [PostgresFact]
    public async Task InvalidatorsCommitFirstAndActualHandlerRefusesTheWrite()
    {
        foreach (var invalidation in Enum.GetValues<Invalidation>())
        {
            await using var fixture = await Database.CreateAsync();
            await using var invalidator = fixture.Context();
            await using var first = await invalidator.Database.BeginTransactionAsync();
            await Invalidate(invalidator, fixture, invalidation);
            await using var profile = fixture.Context();
            await profile.Database.OpenConnectionAsync();
            var pid = ((NpgsqlConnection)profile.Database.GetDbConnection()).ProcessID;
            // Preload the same tracked aggregate authentication used historically.
            await profile.Users.Include(u => u.Accounts).ThenInclude(a => a.Role)
                .SingleAsync(u => u.Id == fixture.UserId);
            var events = new CapturedEvents();
            var pending = Handler(profile, fixture, events).Handle(new UpdateOwnProfileCommand("Must Not Save"), default);
            await fixture.WaitForLockAsync(pid);
            Assert.False(pending.IsCompleted);
            await first.CommitAsync();
            var rejected = await Assert.ThrowsAsync<AuthException>(() => pending);
            Assert.Equal(invalidation is Invalidation.Session or Invalidation.SessionRotation ? "INVALID_TOKEN" : "FORBIDDEN", rejected.Code);
            Assert.Empty(events.Messages);
            await using var verify = fixture.Context();
            Assert.Equal("Original Name", (await verify.Users.SingleAsync(u => u.Id == fixture.UserId)).FullName);
        }
    }

    [PostgresFact]
    public async Task AuthorizedSaveCommitsFirstAndInvalidatorsWaitUntilCommit()
    {
        foreach (var invalidation in Enum.GetValues<Invalidation>())
        {
            await using var fixture = await Database.CreateAsync();
            await using var profile = fixture.Context();
            var events = new CapturedEvents(pause: true);
            var save = Handler(profile, fixture, events).Handle(new UpdateOwnProfileCommand("Saved First"), default);
            await events.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await using var invalidator = fixture.Context();
            await invalidator.Database.OpenConnectionAsync();
            var pid = ((NpgsqlConnection)invalidator.Database.GetDbConnection()).ProcessID;
            var pending = Invalidate(invalidator, fixture, invalidation);
            await fixture.WaitForLockAsync(pid);
            Assert.False(pending.IsCompleted);
            events.Release.TrySetResult();
            Assert.Equal("Saved First", (await save).FullName);
            await pending;
            var published = Assert.Single(events.Messages);
            Assert.Equal(fixture.AccountId, published.ActorAccountId);
            Assert.Equal(fixture.UserId, published.AffectedUserId);
            await using var verify = fixture.Context();
            Assert.Equal("Saved First", (await verify.Users.SingleAsync(u => u.Id == fixture.UserId)).FullName);
        }
    }

    [PostgresFact]
    public async Task SharedUserAcrossAccountsSerializesNamesAndLastCommittedNameWins()
    {
        await using var fixture = await Database.CreateAsync();
        var otherAccountId = Guid.NewGuid();
        var otherTokenId = Guid.NewGuid();
        await using (var setup = fixture.Context())
        {
            setup.Accounts.Add(new Account { Id = otherAccountId, UserId = fixture.UserId,
                RoleCode = "DRIVER" });
            setup.AuthSessions.Add(fixture.Session(otherAccountId, otherTokenId));
            await setup.SaveChangesAsync();
        }
        await using var first = fixture.Context();
        var firstEvents = new CapturedEvents(pause: true);
        var firstSave = Handler(first, fixture, firstEvents).Handle(new UpdateOwnProfileCommand("First Name"), default);
        await firstEvents.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await using var second = fixture.Context();
        // Preload a stale name before the other Account's save commits.
        await second.Users.SingleAsync(u => u.Id == fixture.UserId);
        await second.Database.OpenConnectionAsync();
        var pid = ((NpgsqlConnection)second.Database.GetDbConnection()).ProcessID;
        var secondEvents = new CapturedEvents();
        var secondActor = new TestCurrentActor(fixture.UserId, "driver", otherAccountId, otherTokenId);
        var secondHandler = new UpdateOwnProfileCommandHandler(new UnitOfWork(second, new UserRepository(second)),
            TestAccess.Service(new CurrentAccessStore(second), secondActor, fixture.Clock), secondActor,
            secondEvents, fixture.Clock);
        var pending = secondHandler.Handle(new UpdateOwnProfileCommand("Last Name"), default);
        await fixture.WaitForLockAsync(pid);
        firstEvents.Release.TrySetResult();
        Assert.Equal("First Name", (await firstSave).FullName);
        Assert.Equal("Last Name", (await pending).FullName);
        Assert.Single(firstEvents.Messages);
        Assert.Equal(otherAccountId, Assert.Single(secondEvents.Messages).ActorAccountId);
        await using var verify = fixture.Context();
        foreach (var pair in new[] { (fixture.AccountId, fixture.TokenId), (otherAccountId, otherTokenId) })
            Assert.Equal("Last Name", (await new CurrentAccessStore(verify).LoadCurrentAsync(pair.Item1, pair.Item2, default))!.User.FullName);
    }

    [PostgresFact]
    public async Task SessionAccountBindingIsImmutableInDatabaseAndRotationPreservesIt()
    {
        await using var fixture = await Database.CreateAsync();
        await using var db = fixture.Context();
        var before = await db.AuthSessions.AsNoTracking().SingleAsync(s => s.Id == fixture.SessionId);
        Assert.True(await new PostgresAuthSessionStore(db, fixture.Clock).RotateAsync(before, Guid.NewGuid(), "new-hash", fixture.Clock.Now.AddHours(1), default));
        var after = await db.AuthSessions.AsNoTracking().SingleAsync(s => s.Id == fixture.SessionId);
        Assert.Equal(before.AccountId, after.AccountId);
        Assert.Equal(before.ExpiresAt, after.ExpiresAt);
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE user_refresh_tokens SET account_id = NULL WHERE id = {fixture.SessionId}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
    }

    private enum Invalidation { Session, SessionRotation, Account, Role, User }

    private static Task Invalidate(AppDbContext db, Database fixture, Invalidation kind) => kind switch
    {
        Invalidation.Session => new PostgresAuthSessionStore(db, fixture.Clock).RevokeAsync(fixture.TokenId, fixture.UserId, "refresh-hash", default),
        Invalidation.SessionRotation => Rotate(db, fixture),
        Invalidation.Account => db.Accounts.Where(a => a.Id == fixture.AccountId).ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, "LOCKED")),
        Invalidation.Role => db.Accounts.Where(a => a.Id == fixture.AccountId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.RoleCode, "BUSINESS_OWNER")),
        _ => db.Users.Where(u => u.Id == fixture.UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, UserStatus.Locked))
    };

    private static async Task Rotate(AppDbContext db, Database fixture)
    {
        var previous = await db.AuthSessions.AsNoTracking().SingleAsync(s => s.Id == fixture.SessionId);
        Assert.True(await new PostgresAuthSessionStore(db, fixture.Clock)
            .RotateAsync(previous, Guid.NewGuid(), "rotated-hash", fixture.Clock.Now.AddHours(1), default));
    }

    private static UpdateOwnProfileCommandHandler Handler(AppDbContext db, Database fixture, IEventPublisher events)
    {
        var actor = new TestCurrentActor(fixture.UserId, "driver", fixture.AccountId, fixture.TokenId);
        return new(new UnitOfWork(db, new UserRepository(db)),
            TestAccess.Service(new CurrentAccessStore(db), actor, fixture.Clock), actor, events, fixture.Clock);
    }

    // The real transactional outbox is tested separately. This seam pauses the real handler
    // after authoritative locks, so PostgreSQL lock observation controls the race deterministically.
    private sealed class CapturedEvents(bool pause = false) : IEventPublisher
    {
        public List<UserProfileNameChanged> Messages { get; } = [];
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task PublishAsync<T>(T message, CancellationToken cancellationToken) where T : class, IIntegrationEventMessage
        {
            Messages.Add(Assert.IsType<UserProfileNameChanged>(message));
            Entered.TrySetResult();
            if (pause) await Release.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        }
        public async Task PublishManyAsync<T>(IEnumerable<T> messages, CancellationToken cancellationToken) where T : class, IIntegrationEventMessage
        {
            foreach (var message in messages) await PublishAsync(message, cancellationToken);
        }
    }

    private sealed class Database(NpgsqlConnection admin, string name, DbContextOptions<AppDbContext> options) : IAsyncDisposable
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid AccountId { get; } = Guid.NewGuid();
        public Guid TokenId { get; } = Guid.NewGuid();
        public Guid SessionId { get; } = Guid.NewGuid();
        public TestClock Clock { get; } = new();
        public AppDbContext Context() => new(options);
        public AuthSession Session(Guid account, Guid token) => new()
        {
            Id = account == AccountId ? SessionId : Guid.NewGuid(), UserId = UserId, AccountId = account,
            AccessTokenId = token, TokenHash = account == AccountId ? "refresh-hash" : token.ToString(),
            AccessExpiresAt = Clock.Now.AddHours(24), ExpiresAt = Clock.Now.AddDays(7), CreatedAt = Clock.Now
        };

        public static async Task<Database> CreateAsync()
        {
            var input = Environment.GetEnvironmentVariable("SMARTPARK_AUTH_TEST_CONNECTION")
                ?? throw new InvalidOperationException("An explicit isolated PostgreSQL test connection is required.");
            var name = "smartpark_profile_guard_" + Guid.NewGuid().ToString("N");
            var admin = new NpgsqlConnection(input);
            await admin.OpenAsync();
            await new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin).ExecuteNonQueryAsync();
            var connection = new NpgsqlConnectionStringBuilder(input) { Database = name };
            var fixture = new Database(admin, name, new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection.ConnectionString).Options);
            try
            {
                await using var db = fixture.Context();
                await ServiceSchema.InitializeAsync(db);
                db.Users.Add(new User
                {
                    Id = fixture.UserId, FullName = "Original Name", Status = UserStatus.Active,
                    Email = "profile-race@example.test", PasswordHash = "unused",
                    Accounts = [new Account { Id = fixture.AccountId, UserId = fixture.UserId,
                        RoleCode = "DRIVER" }]
                });
                db.AuthSessions.Add(fixture.Session(fixture.AccountId, fixture.TokenId));
                await db.SaveChangesAsync();
                return fixture;
            }
            catch { await fixture.DisposeAsync(); throw; }
        }

        public async Task WaitForLockAsync(int pid)
        {
            var deadline = Stopwatch.StartNew();
            while (deadline.Elapsed < TimeSpan.FromSeconds(3))
            {
                await using var command = new NpgsqlCommand("SELECT wait_event_type = 'Lock' FROM pg_stat_activity WHERE pid = @pid", admin);
                command.Parameters.AddWithValue("pid", pid);
                if (await command.ExecuteScalarAsync() is true) return;
                await Task.Yield();
            }
            throw new TimeoutException("The second connection did not enter PostgreSQL's lock wait within the test deadline.");
        }

        public async ValueTask DisposeAsync()
        {
            Assert.Matches("^smartpark_profile_guard_[0-9a-f]{32}$", name);
            await new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)", admin).ExecuteNonQueryAsync();
            await admin.DisposeAsync();
        }
    }
}
