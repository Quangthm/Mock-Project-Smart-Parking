using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.Common.Models.JwT;
using UserService.Application.Services;
using UserService.Application.Usecase.Session;
using UserService.Persistence;
using UserService.Persistence.Repositories;

namespace SmartParking.UserService.Tests;

public sealed class SessionOwnershipPersistenceTests
{
    [PostgresFact]
    public async Task DatabaseRejectsMismatchedOwnershipAndRetargeting()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        await using var db = fixture.Open();
        var clock = new TestClock();
        var user = await SeedAsync(db);
        var accountId = user.Accounts.Single().Id!.Value;
        var otherUser = await SeedAsync(db);
        var otherAccountId = otherUser.Accounts.Single().Id!.Value;
        var session = Session(user.Id!.Value, accountId, clock);
        db.AuthSessions.Add(session);
        await db.SaveChangesAsync();

        var invalid = Session(otherUser.Id!.Value, accountId, clock);
        var mismatch = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO user_refresh_tokens(id,user_id,account_id,access_token_id,token_hash,access_expires_at,expires_at,created_at)
            VALUES({invalid.Id},{invalid.UserId},{invalid.AccountId},{invalid.AccessTokenId},{invalid.TokenHash},
                {invalid.AccessExpiresAt},{invalid.ExpiresAt},{invalid.CreatedAt})
            """));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, mismatch.SqlState);
        Assert.Equal("FK_user_refresh_tokens_accounts_account_id_user_id", mismatch.ConstraintName);

        var retarget = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE user_refresh_tokens SET account_id={otherAccountId}, user_id={otherUser.Id} WHERE id={session.Id}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, retarget.SqlState);
        var changeUser = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE user_refresh_tokens SET user_id={otherUser.Id} WHERE id={session.Id}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, changeUser.SqlState);
        var changeOwner = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE accounts SET user_id={otherUser.Id} WHERE id={accountId}"));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, changeOwner.SqlState);
        var persisted = await db.AuthSessions.AsNoTracking().SingleAsync(s => s.Id == session.Id);
        Assert.Equal(accountId, persisted.AccountId);
        Assert.Equal(user.Id, persisted.UserId);
    }

    [PostgresFact]
    public async Task OverlayRerunPreservesLegacyNullBindingAndRefreshRejectsIt()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        await using var db = fixture.Open();
        var clock = new TestClock();
        var user = await SeedAsync(db);
        var legacy = Session(user.Id!.Value, null, clock);
        db.AuthSessions.Add(legacy);
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync(
            ServiceSchema.FeaturePath("microservices/13-own-profile-session-account.sql")));
        Assert.Null((await db.AuthSessions.AsNoTracking().SingleAsync(s => s.Id == legacy.Id)).AccountId);
        var store = new PostgresAuthSessionStore(db, clock);
        Assert.False(await store.IsActiveAsync(legacy.AccessTokenId, legacy.UserId, default));
        var options = new JwtOptions { Issuer = "ownership-test", Audience = "ownership-test" };
        using var keys = new JwtKeyProvider(options, true);
        var auth = new AuthSessionService(new AccessTokenService(options, keys, store, clock), store, new AuthenticationPolicy(), clock);
        Assert.Equal("INVALID_TOKEN", (await Assert.ThrowsAsync<AuthException>(() => auth.RotateAsync(user, legacy, default))).Code);
        var backfill = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE user_refresh_tokens SET account_id={user.Accounts.Single().Id} WHERE id={legacy.Id}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, backfill.SqlState);
    }

    [PostgresFact]
    public async Task RefreshReadsCurrentAccountDespiteTrackedSnapshotAndPreservesBinding()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        await using var db = fixture.Open();
        var clock = new TestClock();
        var user = await SeedAsync(db);
        var account = user.Accounts.Single();
        var store = new PostgresAuthSessionStore(db, clock);
        var options = new JwtOptions { Issuer = "ownership-test", Audience = "ownership-test" };
        using var keys = new JwtKeyProvider(options, true);
        var auth = new AuthSessionService(new AccessTokenService(options, keys, store, clock), store, new AuthenticationPolicy(), clock);
        var issued = await auth.CreateAsync(user, default);
        var initial = await store.FindByRefreshHashAsync(AuthSessionService.HashRefreshToken(issued.RefreshToken!), default);
        var refresh = new RefreshCommandHandler(new UnitOfWork(db, new UserRepository(db)), store, auth, clock);
        var rotated = await refresh.Handle(new RefreshCommand(issued.RefreshToken), default);
        var current = (await store.FindByRefreshHashAsync(AuthSessionService.HashRefreshToken(rotated.RefreshToken!), default))!;
        Assert.Equal(initial!.AccountId, current.AccountId);
        Assert.Equal(initial.UserId, current.UserId);
        Assert.Equal(initial.ExpiresAt, current.ExpiresAt);

        await using (var invalidator = fixture.Open())
            await invalidator.Accounts.Where(a => a.Id == account.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, "LOCKED"));
        Assert.Equal("ACTIVE", account.Status); // Deliberately stale identity-map instance.
        Assert.Equal("INVALID_TOKEN", (await Assert.ThrowsAsync<AuthException>(() =>
            refresh.Handle(new RefreshCommand(rotated.RefreshToken), default))).Code);
        Assert.Equal(current.AccessTokenId, (await db.AuthSessions.AsNoTracking().SingleAsync(s => s.Id == current.Id)).AccessTokenId);
    }

    private static async Task<User> SeedAsync(AppDbContext db)
    {
        var user = new User
        {
            Id = Guid.NewGuid(), FullName = "Session Owner", Email = $"{Guid.NewGuid():N}@example.test",
            PasswordHash = "unused", Status = UserStatus.Active,
            Accounts = [new Account { Id = Guid.NewGuid(), RoleCode = "DRIVER" }]
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static AuthSession Session(Guid userId, Guid? accountId, TestClock clock) => new()
    {
        Id = Guid.NewGuid(), UserId = userId, AccountId = accountId, AccessTokenId = Guid.NewGuid(),
        TokenHash = Guid.NewGuid().ToString("N"), CreatedAt = clock.Now,
        AccessExpiresAt = clock.Now.AddHours(1), ExpiresAt = clock.Now.AddDays(7)
    };
}
