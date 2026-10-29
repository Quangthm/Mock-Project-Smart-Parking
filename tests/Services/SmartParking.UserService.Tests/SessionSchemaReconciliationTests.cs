using Microsoft.EntityFrameworkCore;
using Npgsql;
using UserService.Persistence;

namespace SmartParking.UserService.Tests;

/// <summary>Exercises the documented deployment order without the legacy test overlays.</summary>
public sealed class SessionSchemaReconciliationTests
{
    [PostgresFact]
    public async Task BaseThenSessionThenOutboxIsRequiredAndLegacySessionsAreNotBackfilled()
    {
        var input = Environment.GetEnvironmentVariable("SMARTPARK_AUTH_TEST_CONNECTION")!;
        var database = "smartpark_auth_test_" + Guid.NewGuid().ToString("N");
        var connectionString = new NpgsqlConnectionStringBuilder(input) { Database = database }.ConnectionString;
        await using var admin = new NpgsqlConnection(input);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin))
            await create.ExecuteNonQueryAsync();
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            async Task Apply(string file)
            {
                await using var command = new NpgsqlCommand(await File.ReadAllTextAsync(
                    Path.Combine(ServiceSchema.Root, "scripts/database", file)), connection);
                await command.ExecuteNonQueryAsync();
            }
            async Task VerifyReadOnly()
            {
                await using var verify = new NpgsqlCommand(await File.ReadAllTextAsync(
                    Path.Combine(ServiceSchema.Root, "scripts/database/verify-user-access-schema.sql")), connection);
                try { await verify.ExecuteNonQueryAsync(); }
                catch (PostgresException)
                {
                    // The verifier owns its read-only transaction. Clear its failed
                    // transaction before applying the next stage in this disposable DB.
                    await using var rollback = new NpgsqlCommand("ROLLBACK", connection);
                    await rollback.ExecuteNonQueryAsync();
                    throw;
                }
            }

            await Apply("microservices/01-user-access-service-db.sql");
            var baseOnly = await Assert.ThrowsAsync<PostgresException>(VerifyReadOnly);
            Assert.Contains("13-own-profile-session-account.sql", baseOnly.Hint);

            // An actual pre-13 session has no Account column. Never infer one on upgrade,
            // even when only one account currently exists for this User.
            var userId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var legacyId = Guid.NewGuid();
            await using (var seed = new NpgsqlCommand("""
                INSERT INTO users(id,password_hash,full_name) VALUES (@user,'unused','Schema fixture');
                INSERT INTO accounts(id,user_id,role_code) VALUES (@account,@user,'DRIVER');
                INSERT INTO user_refresh_tokens(id,user_id,token_hash,access_token_id,access_expires_at,expires_at)
                    VALUES (@session,@user,'legacy-schema-fixture',gen_random_uuid(),now()+interval '1 hour',now()+interval '7 days');
                """, connection))
            {
                seed.Parameters.AddWithValue("user", userId);
                seed.Parameters.AddWithValue("account", accountId);
                seed.Parameters.AddWithValue("session", legacyId);
                await seed.ExecuteNonQueryAsync();
            }

            await Apply("microservices/13-own-profile-session-account.sql");
            await Apply("microservices/13-own-profile-session-account.sql"); // Supported upgrade is repeatable.
            var withoutOutbox = await Assert.ThrowsAsync<PostgresException>(VerifyReadOnly);
            Assert.Contains("14-user-profile-outbox.sql", withoutOutbox.Hint);

            await using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options))
            {
                Assert.Null((await db.AuthSessions.SingleAsync(s => s.Id == legacyId)).AccountId);
                Assert.Equal("DRIVER", (await db.Accounts.Include(a => a.Role).SingleAsync()).Role.Code);
            }
            await using (var rebind = new NpgsqlCommand(
                "UPDATE user_refresh_tokens SET account_id=@account WHERE id=@session", connection))
            {
                rebind.Parameters.AddWithValue("account", accountId);
                rebind.Parameters.AddWithValue("session", legacyId);
                Assert.Equal(PostgresErrorCodes.CheckViolation,
                    (await Assert.ThrowsAsync<PostgresException>(() => rebind.ExecuteNonQueryAsync())).SqlState);
            }

            await Apply("microservices/14-user-profile-outbox.sql");
            await VerifyReadOnly();
            await using var final = new NpgsqlCommand("""
                SELECT to_regclass('public.account_roles') IS NULL
                    AND to_regclass('public."OutboxMessage"') IS NOT NULL
                    AND EXISTS (SELECT 1 FROM information_schema.columns
                        WHERE table_schema='public' AND table_name='user_refresh_tokens' AND column_name='account_id' AND udt_name='uuid');
                """, connection);
            Assert.Equal(true, await final.ExecuteScalarAsync());
        }
        finally
        {
            Assert.Matches("^smartpark_auth_test_[0-9a-f]{32}$", database);
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
            using var pool = new NpgsqlConnection(connectionString);
            NpgsqlConnection.ClearPool(pool);
        }
    }
}
