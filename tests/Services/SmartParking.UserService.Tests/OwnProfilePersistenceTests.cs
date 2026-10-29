using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Interfaces.MessageBroker;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.Profiles.Commands;
using UserService.Application.Profiles.Queries;
using UserService.Persistence;
using UserService.Persistence.Repositories;

namespace SmartParking.UserService.Tests;

/// <summary>Real PostgreSQL profile behavior, separate from deterministic authorization race tests.</summary>
public sealed class OwnProfilePersistenceTests
{
    [PostgresFact]
    public async Task SharedUserNameAndLegacyAvatarRoundTripWithoutChangingOtherFields()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        var clock = new TestClock();
        var seeded = await SeedAsync(fixture, clock);
        await using var provider = ProfileOutboxTests.Services(fixture.ConnectionString).BuildServiceProvider();
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var handler = Handler(db, seeded.Actor, scope.ServiceProvider.GetRequiredService<IEventPublisher>(), clock);
            var response = await handler.Handle(new UpdateOwnProfileCommand("  Nguyễn  An  "), default);
            Assert.Equal("Nguyễn  An", response.FullName);
            Assert.Equal(seeded.Avatar, response.AvatarUrl);
        }

        await using (var db = fixture.Open())
        {
            var user = await db.Users.Include(u => u.Accounts).ThenInclude(a => a.Role).SingleAsync();
            Assert.Equal("Nguyễn  An", user.FullName);
            Assert.Equal(seeded.Avatar, user.AvatarUrl);
            Assert.Equal("profile@example.test", user.Email);
            Assert.Equal("+84912345678", user.Phone);
            Assert.Equal("unchanged-hash", user.PasswordHash);
            Assert.Equal("Unchanged Company", user.CompanyName);
            Assert.Equal(UserStatus.Active, user.Status);
            Assert.Equal(0, user.FailedLoginAttempts);
            Assert.Null(user.LockedUntil);
            Assert.Equal(clock.Now, user.EmailVerifiedAt);
            Assert.Equal(clock.Now, user.PhoneVerifiedAt);
            Assert.Equal(clock.Now, user.CreatedOn);
            Assert.Equal(clock.Now, user.ModifiedOn);
            Assert.Equal(2, user.Accounts.Count);
            Assert.All(user.Accounts, account =>
            {
                Assert.Equal("ACTIVE", account.Status);
                Assert.Empty(account.Permissions!);
                Assert.Equal("DRIVER", account.RoleCode);
            });
            Assert.Equal(2, await db.AuthSessions.CountAsync(s => !s.IsRevoked && s.AccountId != null));
            var throughOtherAccount = await new GetOwnProfileQueryHandler(TestAccess.Service(new CurrentAccessStore(db), seeded.OtherActor, clock))
                .Handle(new GetOwnProfileQuery(), default);
            Assert.Equal("Nguyễn  An", throughOtherAccount.FullName);
            Assert.Equal(seeded.Avatar, throughOtherAccount.AvatarUrl);
        }
    }

    [PostgresFact]
    public async Task ApprovedScalarBoundaryMatchesPostgresCapacityAndRejectsOverflowBeforeMutation()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        var clock = new TestClock();
        var seeded = await SeedAsync(fixture, clock);
        await using var provider = ProfileOutboxTests.Services(fixture.ConnectionString).BuildServiceProvider();
        var accepted = string.Concat(Enumerable.Repeat("\U0001F680", 255));
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var handler = Handler(db, seeded.Actor, scope.ServiceProvider.GetRequiredService<IEventPublisher>(), clock);
            Assert.Equal(accepted, (await handler.Handle(new UpdateOwnProfileCommand(accepted), default)).FullName);
        }
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var handler = Handler(db, seeded.Actor, scope.ServiceProvider.GetRequiredService<IEventPublisher>(), clock);
            await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(new UpdateOwnProfileCommand(accepted + "\U0001F680"), default));
        }
        await using var verify = fixture.Open();
        Assert.Equal(accepted, (await verify.Users.SingleAsync()).FullName);
        Assert.Equal(1, await verify.Set<MassTransit.EntityFrameworkCoreIntegration.OutboxMessage>().CountAsync());
    }

    [PostgresFact]
    public async Task ACurrentLockedAccountIsDeniedDespiteAnotherEligibleAccount()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        var clock = new TestClock();
        var seeded = await SeedAsync(fixture, clock);
        await using (var change = fixture.Open())
            await change.Accounts.Where(a => a.Id == seeded.Actor.AccountId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.Status, "LOCKED"));
        await using var provider = ProfileOutboxTests.Services(fixture.ConnectionString).BuildServiceProvider();
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var handler = Handler(db, seeded.Actor, scope.ServiceProvider.GetRequiredService<IEventPublisher>(), clock);
            Assert.Equal("FORBIDDEN", (await Assert.ThrowsAsync<AuthException>(
                () => handler.Handle(new UpdateOwnProfileCommand("Forbidden"), default))).Code);
        }
        await using var verify = fixture.Open();
        Assert.Equal("Original", (await verify.Users.SingleAsync()).FullName);
        Assert.Empty(await verify.Set<MassTransit.EntityFrameworkCoreIntegration.OutboxMessage>().ToListAsync());
    }

    private static UpdateOwnProfileCommandHandler Handler(AppDbContext db, TestCurrentActor actor,
        IEventPublisher events, TimeProvider clock) =>
        new(new UnitOfWork(db, new UserRepository(db)), TestAccess.Service(new CurrentAccessStore(db), actor, clock), actor, events, clock);

    private static async Task<(TestCurrentActor Actor, TestCurrentActor OtherActor, string Avatar)> SeedAsync(
        UserServiceDatabaseFixture fixture, TestClock clock)
    {
        await using var db = fixture.Open();
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var otherAccountId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var otherTokenId = Guid.NewGuid();
        var avatar = "  https://legacy.example.test/" + new string('a', 300) + "  ";
        db.Users.Add(new User
        {
            Id = userId, FullName = "Original", Email = "profile@example.test", Phone = "+84912345678",
            PasswordHash = "unchanged-hash", CompanyName = "Unchanged Company", AvatarUrl = avatar,
            Status = UserStatus.Active, EmailVerifiedAt = clock.Now, PhoneVerifiedAt = clock.Now,
            CreatedOn = clock.Now, ModifiedOn = clock.Now,
            Accounts = new[] { accountId, otherAccountId }.Select(id => new Account
            {
                Id = id, UserId = userId, Status = "ACTIVE", Permissions = [],
                RoleCode = "DRIVER"
            }).ToArray()
        });
        foreach (var (account, token) in new[] { (accountId, tokenId), (otherAccountId, otherTokenId) })
            db.AuthSessions.Add(new AuthSession
            {
                Id = Guid.NewGuid(), UserId = userId, AccountId = account, AccessTokenId = token,
                TokenHash = token.ToString("N"), CreatedAt = clock.Now,
                AccessExpiresAt = clock.Now.AddHours(1), ExpiresAt = clock.Now.AddDays(1)
            });
        await db.SaveChangesAsync();
        return (new(userId, "driver", accountId, tokenId), new(userId, "driver", otherAccountId, otherTokenId), avatar);
    }
}
