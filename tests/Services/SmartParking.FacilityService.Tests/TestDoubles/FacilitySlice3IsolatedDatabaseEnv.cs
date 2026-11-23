using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SmartParking.FacilityService.Application;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Reservations;
using SmartParking.FacilityService.Infrastructure;
using SmartParking.FacilityService.Infrastructure.Messaging;
using SmartParking.FacilityService.Persistence;
using Xunit;

namespace SmartParking.FacilityService.Tests.TestDoubles;

internal sealed partial class FacilitySlice3IsolatedDatabaseEnv : IAsyncDisposable
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

    private readonly string _adminConn;
    private readonly string _dbConn;
    private readonly string _dbName;

    public NpgsqlDataSource DataSource { get; }
    public Guid TenantId { get; } = Guid.NewGuid();
    public Guid ActorId { get; } = Guid.NewGuid();
    public Guid SiteId { get; } = Guid.NewGuid();
    public Guid UnitId { get; } = Guid.NewGuid();
    public Guid SlotId { get; } = Guid.NewGuid();

    private FacilitySlice3IsolatedDatabaseEnv(string adminConn, string dbConn, string dbName, NpgsqlDataSource dataSource)
    {
        _adminConn = adminConn;
        _dbConn = dbConn;
        _dbName = dbName;
        DataSource = dataSource;
    }

    public static async Task<FacilitySlice3IsolatedDatabaseEnv> CreateAsync()
    {
        var admin = Environment.GetEnvironmentVariable("SMARTPARK_STRUCTURE_TEST_CONNECTION")!;
        var dbName = "facility_outbox_test_" + Guid.NewGuid().ToString("N");
        if (!IsolatedDbNamePattern().IsMatch(dbName) || ProtectedDatabases.Contains(dbName))
            throw new InvalidOperationException($"Unsafe test DB name '{dbName}'.");

        var host = (new NpgsqlConnectionStringBuilder(admin).Host ?? string.Empty).Trim();
        if (!new[] { "127.0.0.1", "localhost", "::1" }.Contains(host, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Refusing non-local PostgreSQL host '{host}'.");

        await using (var adminConn = new NpgsqlConnection(admin))
        {
            await adminConn.OpenAsync();
            await new NpgsqlCommand($"CREATE DATABASE {dbName}", adminConn).ExecuteNonQueryAsync();
        }

        var dbConn = new NpgsqlConnectionStringBuilder(admin) { Database = dbName }.ConnectionString;
        var dataSource = NpgsqlDataSource.Create(dbConn);
        var env = new FacilitySlice3IsolatedDatabaseEnv(admin, dbConn, dbName, dataSource);
        try
        {
            var root = FindRepositoryRoot();
            foreach (var rel in new[]
            {
                "scripts/database/microservices/02-facility-service-db.sql",
                "scripts/database/microservices/03a-parking-structure-ops-service-db.sql",
                "scripts/database/integration/realtime-projection.sql",
                "scripts/database/integration/facility-transactional-outbox.sql"
            })
            {
                await using var conn = await dataSource.OpenConnectionAsync();
                var sql = await File.ReadAllTextAsync(Path.Combine(root, rel));
                await new NpgsqlCommand(sql, conn).ExecuteNonQueryAsync();
            }

            await env.SeedAsync();
            return env;
        }
        catch
        {
            await env.DisposeAsync();
            throw;
        }
    }

    private async Task SeedAsync()
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        await new NpgsqlCommand($"""
            INSERT INTO tenants(id, code, name, contact_email, contact_phone, status)
            VALUES ('{TenantId}', 'T_S3', 'Slice 3 Tenant', 's3@example.com', '0000000000', 'ACTIVE');
            INSERT INTO parking_sites(id, tenant_id, site_code, name, address, status)
            VALUES ('{SiteId}', '{TenantId}', 'S3', 'Slice 3 Site', 'Address', 'ACTIVE');
            INSERT INTO spatial_units(id, tenant_id, site_id, path, unit_type, name, max_capacity)
            VALUES ('{UnitId}', '{TenantId}', '{SiteId}', '/{SiteId}/{UnitId}/', 'ZONE', 'Zone A', 10);
            INSERT INTO parking_slots(id, tenant_id, site_id, spatial_unit_id, slot_code, supported_vehicle_type, slot_type, physical_state)
            VALUES ('{SlotId}', '{TenantId}', '{SiteId}', '{UnitId}', 'A01', 'CAR', 'STANDARD', 'AVAILABLE');
            """, conn).ExecuteNonQueryAsync();
    }

    public ServiceProvider BuildProvider(bool outboxDeliveryEnabled, string? rabbitMqConnection)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(DataSource);
        services.AddDbContext<ParkingDbContext>(options => options.UseNpgsql(DataSource));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IParkingStructureRepository, PostgresParkingStructureRepository>();
        services.AddScoped<IMaintenanceWindowStore, PostgresMaintenanceWindowStore>();
        services.AddScoped<IOccupancyProjectionRepository, OccupancyProjectionRepository>();
        services.AddScoped<IReservationDependencyRepository, ReservationDependencyRepository>();
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

    public async Task<Guid> PrepareSynchronizationAsync(Guid siteId)
    {
        var token = Guid.NewGuid();
        await using var conn = await DataSource.OpenConnectionAsync();
        await new NpgsqlCommand(
            $"INSERT INTO structure_synchronizations(token, tenant_id, site_id, state) VALUES ('{token}', '{TenantId}', '{siteId}', 'preparing')",
            conn).ExecuteNonQueryAsync();
        return token;
    }

    public async Task<long> CountOutboxMessagesAsync() =>
        await ScalarLongAsync("SELECT count(*) FROM \"OutboxMessage\"");

    public async Task<(Guid OutboxId, string Body)> ReadLatestOutboxAsync()
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("SELECT \"OutboxId\", \"Body\" FROM \"OutboxMessage\" ORDER BY \"SequenceNumber\" DESC LIMIT 1", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        Assert.True(await r.ReadAsync());
        return (r.GetGuid(0), r.GetString(1));
    }

    public async Task<long> ScalarLongAsync(string sql)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        return Convert.ToInt64(await new NpgsqlCommand(sql, conn).ExecuteScalarAsync());
    }

    public async Task<string?> ScalarStringAsync(string sql)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        return await new NpgsqlCommand(sql, conn).ExecuteScalarAsync() as string;
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "SmartParking.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Repository root missing.");
    }

    public async ValueTask DisposeAsync()
    {
        await DataSource.DisposeAsync();
        await using (var poolConn = new NpgsqlConnection(_dbConn))
            NpgsqlConnection.ClearPool(poolConn);
        await using var adminConn = new NpgsqlConnection(_adminConn);
        await adminConn.OpenAsync();
        await new NpgsqlCommand($"DROP DATABASE IF EXISTS {_dbName} WITH (FORCE)", adminConn).ExecuteNonQueryAsync();
    }
}
