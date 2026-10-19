using Npgsql;
using SmartParking.ParkingService.Persistence;

namespace SmartParking.ParkingService.Tests;

public sealed class GateAuthenticationTests
{
    [StructurePostgresFact]
    public async Task GatePoolPreservesConfiguredPasswordDuringAuthentication()
    {
        var input = Environment.GetEnvironmentVariable("SMARTPARK_STRUCTURE_TEST_CONNECTION")!;
        var suffix = Guid.NewGuid().ToString("N");
        var role = "gate_auth_role_" + suffix;
        var database = "gate_auth_test_" + suffix;
        var password = Guid.NewGuid().ToString("N");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var admin = new NpgsqlConnection(input);
        await admin.OpenAsync(deadline.Token);

        // Only generated identifiers/credentials are interpolated. A dedicated
        // login verifies password authentication without changing application users.
        await using (var createRole = new NpgsqlCommand(
            $"CREATE ROLE {role} LOGIN PASSWORD '{password}'", admin))
            await createRole.ExecuteNonQueryAsync(deadline.Token);
        try
        {
            await using (var createDatabase = new NpgsqlCommand(
                $"CREATE DATABASE {database} OWNER {role}", admin))
                await createDatabase.ExecuteNonQueryAsync(deadline.Token);

            var settings = new NpgsqlConnectionStringBuilder(input)
            {
                Database = database,
                Username = role,
                Password = password,
                PersistSecurityInfo = false,
                Timeout = 5,
                CommandTimeout = 5
            };

            // Reject trust/peer configurations: success alone would not prove
            // that the pool preserved the configured password.
            var incorrect = new NpgsqlConnectionStringBuilder(settings.ConnectionString)
            {
                Password = Guid.NewGuid().ToString("N")
            };
            await using (var rejectedPool = new ParkingGateConnectionPool(incorrect.ConnectionString, 2))
            {
                var error = await Assert.ThrowsAsync<PostgresException>(async () =>
                {
                    await using var unexpected = await rejectedPool.TryOpenAsync(deadline.Token);
                });
                Assert.Equal(PostgresErrorCodes.InvalidPassword, error.SqlState);
            }

            await using var gates = new ParkingGateConnectionPool(settings.ConnectionString, 2);
            // Reopen after disposal to exercise both authentication and lease reuse.
            for (var attempt = 0; attempt < 2; attempt++)
            {
                await using var lease = await gates.TryOpenAsync(deadline.Token);
                Assert.NotNull(lease);
                await using var identity = new NpgsqlCommand(
                    "SELECT current_user, current_database(), current_setting('application_name')",
                    lease.Connection);
                await using (var reader = await identity.ExecuteReaderAsync(deadline.Token))
                {
                    Assert.True(await reader.ReadAsync(deadline.Token));
                    Assert.Equal(role, reader.GetString(0));
                    Assert.Equal(database, reader.GetString(1));
                    Assert.Equal("SmartParking.SessionGates", reader.GetString(2));
                }

                await using var acquire = new NpgsqlCommand(
                    "SELECT pg_try_advisory_lock(12345::bigint)", lease.Connection);
                Assert.Equal(true, await acquire.ExecuteScalarAsync(deadline.Token));
                await using var release = new NpgsqlCommand(
                    "SELECT pg_advisory_unlock(12345::bigint)", lease.Connection);
                Assert.Equal(true, await release.ExecuteScalarAsync(deadline.Token));
            }
        }
        finally
        {
            // Clean only this test's database and role, including on assertion failure.
            try
            {
                await using var dropDatabase = new NpgsqlCommand(
                    $"DROP DATABASE IF EXISTS {database} WITH (FORCE)", admin);
                await dropDatabase.ExecuteNonQueryAsync();
            }
            finally
            {
                await using var dropRole = new NpgsqlCommand($"DROP ROLE {role}", admin);
                await dropRole.ExecuteNonQueryAsync();
            }
        }
    }
}
