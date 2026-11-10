using Npgsql;
using SmartParking.FacilityService.Application;
using SmartParking.FacilityService.Persistence;

namespace SmartParking.FacilityService.Tests;

// SPARK-244 G5 coverage for the facility-owned projection ledger. These are integration tests: the
// release-once SQL only behaves correctly against real PostgreSQL constraints (ON CONFLICT on the
// unique ledger index, FOR UPDATE watermarks), so they need SMARTPARK_STRUCTURE_TEST_CONNECTION
// pointing at an isolated server with CREATE DATABASE. Until that variable is set they are skipped
// through the shared [StructurePostgresFact] attribute (declared in PostgresStructureWorkflowTests).
public sealed class OccupancyProjectionRepositoryTests
{
    [StructurePostgresFact]
    public async Task DuplicateEventIdIsAppliedOnce()
    {
        await using var fixture = await OccupancyProjectionFixture.CreateAsync();
        var store = new OccupancyProjectionRepository(fixture.DataSource);
        var eventId = Guid.NewGuid();
        var change = new OccupancyProjectionEvent(
            EventId: eventId,
            EventType: "SlotStateChanged",
            OccurredAt: DateTimeOffset.UtcNow,
            SequenceNumber: 1,
            SiteId: fixture.SiteId,
            SlotId: fixture.SlotId,
            SessionId: null,
            PhysicalState: "OCCUPIED");

        // First delivery projects the change.
        Assert.True(await store.ApplyAsync(change, CancellationToken.None));
        // Replaying the same eventId rolls back before any projection runs and reports false.
        Assert.False(await store.ApplyAsync(change, CancellationToken.None));

        Assert.Equal(1L, await fixture.ProcessedEventCountAsync(eventId));
        Assert.Equal("OCCUPIED", await fixture.SlotStateAsync());
    }

    [StructurePostgresFact]
    public async Task SpentReleaseLedgerDoesNotFreeASlotReoccupiedByAnotherSession()
    {
        await using var fixture = await OccupancyProjectionFixture.CreateAsync();
        var store = new OccupancyProjectionRepository(fixture.DataSource);
        var sessionA = Guid.NewGuid();

        // Session A occupies the slot, then releases it: the ledger records session A once.
        Assert.True(await store.ApplyAsync(Change(fixture, "OCCUPIED", 1), CancellationToken.None));
        Assert.True(await store.ApplyAsync(Release(fixture, sessionA, sequenceNumber: 2), CancellationToken.None));
        Assert.Equal("AVAILABLE", await fixture.SlotStateAsync());
        Assert.Equal(1L, await fixture.ReleaseLedgerSessionCountAsync(sessionA));

        // A new session B occupies the same slot in a later sequence.
        Assert.True(await store.ApplyAsync(Change(fixture, "OCCUPIED", 3), CancellationToken.None));
        Assert.Equal("OCCUPIED", await fixture.SlotStateAsync());

        // The release advisory is redelivered for the already-spent session A. NOTE: the store
        // returns true here because the event is fresh and does advance the watermark; the
        // release-once guarantee is therefore asserted on database state, not on this return value.
        Assert.True(await store.ApplyAsync(Release(fixture, sessionA, sequenceNumber: 4), CancellationToken.None));

        Assert.Equal("OCCUPIED", await fixture.SlotStateAsync());
        Assert.Equal(1L, await fixture.ReleaseLedgerSessionCountAsync(sessionA));
    }

    [StructurePostgresFact]
    public async Task ReleaseWithoutSlotIsRecordedButNotProjected()
    {
        await using var fixture = await OccupancyProjectionFixture.CreateAsync();
        var store = new OccupancyProjectionRepository(fixture.DataSource);
        var session = Guid.NewGuid();

        Assert.True(await store.ApplyAsync(Change(fixture, "OCCUPIED", 1), CancellationToken.None));

        // Walk-in occupancy: no slot to project onto. The event is recorded and committed, then
        // reported as not applied, and no ledger row is written.
        var releaseEventId = Guid.NewGuid();
        var walkInRelease = new OccupancyProjectionEvent(
            EventId: releaseEventId,
            EventType: "OccupancyReleased",
            OccurredAt: DateTimeOffset.UtcNow,
            SequenceNumber: 2,
            SiteId: fixture.SiteId,
            SlotId: null,
            SessionId: session,
            PhysicalState: null);

        Assert.False(await store.ApplyAsync(walkInRelease, CancellationToken.None));

        Assert.Equal(1L, await fixture.ProcessedEventCountAsync(releaseEventId));
        Assert.Equal(0L, await fixture.ScalarLongAsync("SELECT count(*) FROM occupancy_release_ledger"));
        Assert.Equal("OCCUPIED", await fixture.SlotStateAsync());
    }

    [StructurePostgresFact]
    public async Task StaleSequenceIsIgnoredAfterTheWatermarkAdvances()
    {
        await using var fixture = await OccupancyProjectionFixture.CreateAsync();
        var store = new OccupancyProjectionRepository(fixture.DataSource);

        Assert.True(await store.ApplyAsync(Change(fixture, "OCCUPIED", 10), CancellationToken.None));
        Assert.Equal("OCCUPIED", await fixture.SlotStateAsync());

        // A late redelivery with a lower sequence for the same slot must not regress the projection.
        Assert.False(await store.ApplyAsync(Change(fixture, "MAINTENANCE", 5), CancellationToken.None));

        Assert.Equal("OCCUPIED", await fixture.SlotStateAsync());
        Assert.Equal(10L, await fixture.WatermarkAsync());
    }

    private static OccupancyProjectionEvent Change(OccupancyProjectionFixture fixture, string physicalState, long sequenceNumber)
        => new(Guid.NewGuid(), "SlotStateChanged", DateTimeOffset.UtcNow, sequenceNumber,
            fixture.SiteId, fixture.SlotId, null, physicalState);

    private static OccupancyProjectionEvent Release(OccupancyProjectionFixture fixture, Guid sessionId, long sequenceNumber)
        => new(Guid.NewGuid(), "OccupancyReleased", DateTimeOffset.UtcNow, sequenceNumber,
            fixture.SiteId, fixture.SlotId, sessionId, null);

    // One throwaway database per test: the G5 behaviour depends on real constraints, and isolation
    // keeps tests independent (and safe to run in parallel). DDL is loaded from the repository files
    // so the schema under test is the shipped one, never a hand-copied subset.
    private sealed class OccupancyProjectionFixture : IAsyncDisposable
    {
        private static readonly string[] DdlFiles =
        [
            "scripts/database/microservices/02-facility-service-db.sql",
            "scripts/database/integration/realtime-projection.sql"
        ];

        private readonly string adminConnection;
        private readonly string databaseName;

        public NpgsqlDataSource DataSource { get; }
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid SiteId { get; } = Guid.NewGuid();
        public Guid UnitId { get; } = Guid.NewGuid();
        public Guid SlotId { get; } = Guid.NewGuid();

        private OccupancyProjectionFixture(string admin, string database)
        {
            adminConnection = admin;
            databaseName = database;
            var settings = new NpgsqlConnectionStringBuilder(admin) { Database = database };
            DataSource = NpgsqlDataSource.Create(settings.ConnectionString);
        }

        public static async Task<OccupancyProjectionFixture> CreateAsync()
        {
            var admin = Environment.GetEnvironmentVariable("SMARTPARK_STRUCTURE_TEST_CONNECTION")
                ?? throw new InvalidOperationException(
                    "SMARTPARK_STRUCTURE_TEST_CONNECTION must point at an isolated PostgreSQL server with CREATE DATABASE permission.");
            // Only a fixed prefix and our own random GUID are interpolated, never user input.
            var database = "occupancy_projection_test_" + Guid.NewGuid().ToString("N");
            await using (var connection = new NpgsqlConnection(admin))
            {
                await connection.OpenAsync();
                await new NpgsqlCommand($"CREATE DATABASE {database}", connection).ExecuteNonQueryAsync();
            }

            var fixture = new OccupancyProjectionFixture(admin, database);
            try
            {
                var root = FindRepositoryRoot();
                foreach (var file in DdlFiles)
                    await fixture.ExecuteAsync(await File.ReadAllTextAsync(Path.Combine(root, file)));
                await fixture.SeedAsync();
                return fixture;
            }
            catch
            {
                await fixture.DisposeAsync();
                throw;
            }
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "SmartParking.slnx"))) return directory.FullName;
                directory = directory.Parent;
            }
            throw new InvalidOperationException("Repository root is not available.");
        }

        // Seeds the minimum facility aggregate the store projects onto. Every FK and NOT NULL
        // declared by 02-facility-service-db.sql / parking-tenants.sql is satisfied.
        private async Task SeedAsync() => await ExecuteAsync($"""
            INSERT INTO tenants(id, code, name, contact_email, contact_phone)
            VALUES ('{TenantId}', 'OCC', 'Occupancy Test', 'occupancy@example.com', '0000000000');

            INSERT INTO parking_sites(id, tenant_id, site_code, name, address)
            VALUES ('{SiteId}', '{TenantId}', 'OCC', 'Occupancy Test', 'Test Address');

            INSERT INTO spatial_units(id, tenant_id, site_id, path, unit_type, name)
            VALUES ('{UnitId}', '{TenantId}', '{SiteId}', 'A', 'ZONE', 'A');

            INSERT INTO parking_slots(id, tenant_id, site_id, spatial_unit_id, slot_code, physical_state)
            VALUES ('{SlotId}', '{TenantId}', '{SiteId}', '{UnitId}', 'A01', 'AVAILABLE');
            """);

        public async Task ExecuteAsync(string sql)
        {
            await using var connection = await DataSource.OpenConnectionAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }

        public Task<long> ProcessedEventCountAsync(Guid eventId)
            => ScalarLongAsync("SELECT count(*) FROM processed_events WHERE event_id = @id",
                parameters => parameters.AddWithValue("id", eventId));

        public Task<long> ReleaseLedgerSessionCountAsync(Guid sessionId)
            => ScalarLongAsync("SELECT count(*) FROM occupancy_release_ledger WHERE session_id = @id",
                parameters => parameters.AddWithValue("id", sessionId));

        public Task<string?> SlotStateAsync()
            => ScalarStringAsync("SELECT physical_state FROM parking_slots WHERE id = @id",
                parameters => parameters.AddWithValue("id", SlotId));

        public Task<long> WatermarkAsync()
            => ScalarLongAsync("SELECT last_sequence_number FROM slot_sequence_watermarks WHERE slot_id = @id",
                parameters => parameters.AddWithValue("id", SlotId));

        public async Task<long> ScalarLongAsync(string sql, Action<NpgsqlParameterCollection>? bind = null)
            => Convert.ToInt64(await QueryScalarAsync(sql, bind));

        private async Task<string?> ScalarStringAsync(string sql, Action<NpgsqlParameterCollection>? bind = null)
            => await QueryScalarAsync(sql, bind) as string;

        private async Task<object?> QueryScalarAsync(string sql, Action<NpgsqlParameterCollection>? bind)
        {
            await using var connection = await DataSource.OpenConnectionAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            bind?.Invoke(command.Parameters);
            return await command.ExecuteScalarAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await DataSource.DisposeAsync();
            await using var connection = new NpgsqlConnection(adminConnection);
            await connection.OpenAsync();
            // Only the generated-name database created by this fixture.
            await new NpgsqlCommand($"DROP DATABASE IF EXISTS {databaseName} WITH (FORCE)", connection)
                .ExecuteNonQueryAsync();
        }
    }
}
