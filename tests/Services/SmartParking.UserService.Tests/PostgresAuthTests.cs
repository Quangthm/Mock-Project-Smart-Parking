using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Models.JwT;
using UserService.Application.Services;
using UserService.Application.Usecase.Login;
using UserService.Application.Usecase.Session;
using UserService.Infrastructure.Services;
using UserService.Persistence;
using UserService.Persistence.Repositories;

namespace SmartParking.UserService.Tests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SMARTPARK_AUTH_TEST_CONNECTION")))
            Skip = "Set SMARTPARK_AUTH_TEST_CONNECTION to an isolated PostgreSQL server to run persistence checks.";
    }
}

public sealed class PostgresAuthTests
{
    [PostgresFact]
    public async Task LoginUsesReplacementUserWhenDeletedUserHasSameEmail()
    {
        var input = Environment.GetEnvironmentVariable("SMARTPARK_AUTH_TEST_CONNECTION")!;
        var connection = new NpgsqlConnectionStringBuilder(input);
        var database = "smartpark_auth_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(input);
        await admin.OpenAsync();
        await new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin).ExecuteNonQueryAsync();
        connection.Database = database;
        try
        {
            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connection.ConnectionString).Options);
            await ServiceSchema.InitializeAsync(db);
            var passwords = new BcryptPasswordService();
            await new DataSeeder(db, passwords).SeedAsync();
            var oldUser = await db.Users.SingleAsync();
            oldUser.DeletedOn = DateTimeOffset.UtcNow;
            var replacementId = Guid.Parse("22222222-2222-2222-2222-222222222222");
            db.Users.Add(new User
            {
                Id = replacementId, Email = oldUser.Email, Phone = "0000000000", FullName = "Replacement user",
                PasswordHash = passwords.Hash("Replacement@123"), Status = UserStatus.Active,
                CreatedOn = DateTimeOffset.UtcNow
            });
            // The replacement account has the same role; the old user remains as deleted history.
            foreach (var account in await db.Accounts.ToListAsync()) account.UserId = replacementId;
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var users = new UserRepository(db);
            Assert.Equal(replacementId, (await users.GetByEmailAsync("driver@gmail.com", default))!.Id);
            var clock = new TestClock();
            var policy = new AuthenticationPolicy();
            var jwt = new JwtOptions { Issuer = "postgres-test", Audience = "postgres-test" };
            using var keys = new JwtKeyProvider(jwt, true);
            var sessions = new PostgresAuthSessionStore(db, clock);
            var tokens = new AccessTokenService(jwt, keys, sessions, clock);
            var handler = new LoginCommandHandler(new UnitOfWork(db, users), passwords,
                new(tokens, sessions, policy, clock), policy, clock);
            var result = await handler.Handle(new LoginCommand { Email = "driver@gmail.com", Password = "Replacement@123" }, default);
            Assert.True(result.IsSuccess);
            Assert.Equal(replacementId, result.Session!.User!.UserId);
            Assert.True(await tokens.ValidateAccessTokenAsync(result.Session.AccessToken!));
            // A deleted user with matching credentials must never become a fallback.
            await db.Users.Where(u => u.Id == replacementId).ExecuteUpdateAsync(setters =>
                setters.SetProperty(u => u.DeletedOn, (DateTimeOffset?)clock.Now));
            db.ChangeTracker.Clear();
            Assert.Null(await users.GetByEmailAsync("driver@gmail.com", default));
            Assert.Equal("AUTH_FAILED", (await handler.Handle(new LoginCommand
                { Email = "driver@gmail.com", Password = "Password@123" }, default)).ErrorCode);
            Assert.Equal(0, (await db.Users.SingleAsync(u => u.Id == oldUser.Id)).FailedLoginAttempts);
        }
        finally
        {
            Assert.Matches("^smartpark_auth_test_[0-9a-f]{32}$", database);
            await new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE)", admin).ExecuteNonQueryAsync();
        }
    }

    [PostgresFact]
    public async Task MigrationConcurrencyRotationRevocationAndRestartUseRealPostgres()
    {
        var input = Environment.GetEnvironmentVariable("SMARTPARK_AUTH_TEST_CONNECTION")!;
        var connection = new NpgsqlConnectionStringBuilder(input);
        var database = "smartpark_auth_test_" + Guid.NewGuid().ToString("N");
        // Only this freshly generated test database is ever dropped.
        await using var admin = new NpgsqlConnection(input);
        await admin.OpenAsync();
        await new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin).ExecuteNonQueryAsync();
        connection.Database = database;
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection.ConnectionString).Options;
        var clock = new TestClock();
        var policy = new AuthenticationPolicy();
        var jwt = new JwtOptions { Issuer = "postgres-test", Audience = "postgres-test" };
        using var keys = new JwtKeyProvider(jwt, true);
        var passwords = new BcryptPasswordService();
        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "SmartParking.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        try
        {
            await using (var db = new AppDbContext(options))
            {
                await ServiceSchema.InitializeAsync(db);
                await new DataSeeder(db, passwords).SeedAsync();
                // Exercise upgrade from the old schema with an existing refresh row.
                await db.Database.ExecuteSqlRawAsync("INSERT INTO user_refresh_tokens(id,user_id,access_token_id,access_expires_at,token_hash,expires_at,is_revoked,created_at) VALUES(gen_random_uuid(), '11111111-1111-1111-1111-111111111111',gen_random_uuid(),NOW(),'legacy-hash',NOW()+INTERVAL '7 days',false,NOW()); ALTER TABLE users DROP COLUMN failed_login_attempts; ALTER TABLE users DROP COLUMN locked_until; ALTER TABLE user_refresh_tokens DROP COLUMN access_token_id; ALTER TABLE user_refresh_tokens DROP COLUMN access_expires_at;");
                var migration = await File.ReadAllTextAsync(Path.Combine(root!.FullName, "scripts/database/05.2-Auth-Design-Alignment.sql"));
                await db.Database.ExecuteSqlRawAsync(migration);
                await db.Database.ExecuteSqlRawAsync(migration); // Idempotence.
                db.ChangeTracker.Clear();
                Assert.True((await db.AuthSessions.SingleAsync()).IsRevoked);
                // Only the public known plaintext seed is converted; a second run is a no-op.
                await db.Database.ExecuteSqlRawAsync("UPDATE users SET password_hash='Password@123' WHERE id='11111111-1111-1111-1111-111111111111'");
                var demoMigration = await File.ReadAllTextAsync(Path.Combine(root.FullName, "scripts/database/05.3-Demo-Password-Hash.sql"));
                await db.Database.ExecuteSqlRawAsync(demoMigration);
                var converted = await db.Users.AsNoTracking().SingleAsync();
                Assert.True(passwords.Verify("Password@123", converted.PasswordHash));
                await db.Database.ExecuteSqlRawAsync(demoMigration);
                Assert.Equal(converted.PasswordHash, (await db.Users.AsNoTracking().SingleAsync()).PasswordHash);
            }

            async Task<LoginResult> Attempt(string password)
            {
                await using var db = new AppDbContext(options);
                var store = new PostgresAuthSessionStore(db, clock);
                var tokenService = new AccessTokenService(jwt, keys, store, clock);
                var uow = new UnitOfWork(db, new UserRepository(db));
                return await new LoginCommandHandler(uow, passwords, new(tokenService, store, policy, clock), policy, clock)
                    .Handle(new LoginCommand { Email = "driver@gmail.com", Password = password }, default);
            }
            var attempts = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Task.Run(() => Attempt("wrong"))));
            Assert.Equal(2, attempts.Count(r => r.ErrorCode == "AUTH_FAILED"));
            Assert.Single(attempts, r => r.ErrorCode == "ACCOUNT_LOCKED");
            await using (var db = new AppDbContext(options))
            {
                var user = await db.Users.SingleAsync();
                Assert.Equal(3, user.FailedLoginAttempts);
                Assert.Equal(UserStatus.Locked, user.Status);
            }
            Assert.Equal("ACCOUNT_LOCKED", (await Attempt("Password@123")).ErrorCode);
            // Expire the persisted lock without moving JWT issuance into the future.
            await using (var db = new AppDbContext(options))
            {
                await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(setters =>
                    setters.SetProperty(u => u.LockedUntil, clock.Now.AddSeconds(-1)));
            }
            var first = (await Attempt("Password@123")).Session!;
            Assert.NotNull(first);
            var second = (await Attempt("Password@123")).Session!;
            // New DbContext/store simulates an API restart with the same signing key.
            await using (var db = new AppDbContext(options))
            {
                var store = new PostgresAuthSessionStore(db, clock);
                var tokenService = new AccessTokenService(jwt, keys, store, clock);
                Assert.True(await tokenService.ValidateAccessTokenAsync(second.AccessToken!));
                Assert.Equal(0, (await db.Users.SingleAsync()).FailedLoginAttempts);
                var hash = AuthSessionService.HashRefreshToken(first.RefreshToken!);
                var snapshot = (await store.FindByRefreshHashAsync(hash, default))!;
                var expires = snapshot.ExpiresAt;
                // Two independent contexts attempt a compare-and-swap on the same snapshot.
                async Task<bool> Rotate()
                {
                    await using var otherDb = new AppDbContext(options);
                    return await new PostgresAuthSessionStore(otherDb, clock).RotateAsync(snapshot, Guid.NewGuid(),
                        Guid.NewGuid().ToString(), clock.Now.AddHours(1), default);
                }
                var winners = await Task.WhenAll(Rotate(), Rotate());
                Assert.Single(winners, value => value);
                Assert.False(await tokenService.ValidateAccessTokenAsync(first.AccessToken!));
                Assert.Null(await store.FindByRefreshHashAsync(hash, default));
                Assert.Equal(expires, (await db.AuthSessions.AsNoTracking().SingleAsync(s => s.Id == snapshot.Id)).ExpiresAt);
                var id = Guid.Parse(new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(second.AccessToken).Id);
                Assert.False(await store.RevokeAsync(id, userId, "wrong-hash", default));
                await new LogoutCommandHandler(store).Handle(new(id, userId, second.RefreshToken), default);
            }
            await using (var db = new AppDbContext(options))
            {
                var store = new PostgresAuthSessionStore(db, clock);
                Assert.False(await new AccessTokenService(jwt, keys, store, clock).ValidateAccessTokenAsync(second.AccessToken!));
                Assert.True((await store.FindByRefreshHashAsync(AuthSessionService.HashRefreshToken(second.RefreshToken!), default))!.IsRevoked);
            }
        }
        finally
        {
            // Keep cleanup constrained to the generated database, never the caller's database.
            Assert.Matches("^smartpark_auth_test_[0-9a-f]{32}$", database);
            await new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE)", admin).ExecuteNonQueryAsync();
        }
    }
}
