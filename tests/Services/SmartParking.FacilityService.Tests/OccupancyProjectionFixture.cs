using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartParking.FacilityService.Application.Usecases.Occupancy.Commands.ApplyOccupancyProjection;
using SmartParking.FacilityService.Application;
using SmartParking.FacilityService.Persistence;

namespace SmartParking.FacilityService.Tests;

// One throwaway database per test: the G5 behaviour depends on real constraints, and isolation
// keeps tests independent (and safe to run in parallel). DDL is loaded from the repository files
// so the schema under test is the shipped one, never a hand-copied subset.
internal sealed class OccupancyProjectionFixture : IAsyncDisposable
{
    private static readonly string[] DdlFiles =
    [
        "scripts/database/microservices/02-facility-service-db.sql",
        "scripts/database/microservices/03a-parking-structure-ops-service-db.sql",
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

    public async Task<bool> ProjectAsync(OccupancyProjectionEvent change, CancellationToken ct)
    {
        await using var db = new ParkingDbContext(new DbContextOptionsBuilder<ParkingDbContext>()
            .UseNpgsql(DataSource).Options);
        var handler = new ApplyOccupancyProjectionCommandHandler(new OccupancyProjectionRepository(db), new UnitOfWork(db));
        return await handler.Handle(new(change), ct);
    }

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

    public Task<string?> ReservationStateAsync()
        => ScalarStringAsync("SELECT reservation_state FROM parking_slots WHERE id = @id",
            parameters => parameters.AddWithValue("id", SlotId));

    public Task SetReservationStateAsync(string reservationState)
        => ExecuteAsync($"UPDATE parking_slots SET reservation_state = '{reservationState}' WHERE id = '{SlotId}'");

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
