using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Security.Cryptography;
using UserService.API;
using UserService.API.Services;
using UserService.Persistence;

namespace SmartParking.UserService.Tests;

// A fresh, service-owned database per test; never apply CREATE TABLE to an existing deployment.
internal sealed class UserServiceDatabaseFixture : IAsyncDisposable
{
    private readonly string adminConnection;
    private readonly string name = "smartpark_auth_test_" + Guid.NewGuid().ToString("N");
    public string ConnectionString { get; }
    private UserServiceDatabaseFixture(string connection)
    {
        adminConnection = connection;
        ConnectionString = new NpgsqlConnectionStringBuilder(connection) { Database = name }.ConnectionString;
    }
    public AppDbContext Open() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).Options);

    // Explicit fixture data for tests of existing owners/operators, not onboarding.
    public async Task SeedTenantsAsync(params Guid[] ids)
    {
        await using var db = Open();
        foreach (var id in ids.Distinct())
        {
            var code = "TEST-" + id.ToString("N");
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO tenants(id,code,name,contact_email,contact_phone)
                VALUES ({id},{code},'Fixture tenant','tenant@example.test','0900000000')
                """);
        }
    }

    public static async Task<UserServiceDatabaseFixture> CreateAsync()
    {
        var fixture = new UserServiceDatabaseFixture(Environment.GetEnvironmentVariable("SMARTPARK_AUTH_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Configure the isolated PostgreSQL test server."));
        await using var admin = new NpgsqlConnection(fixture.adminConnection);
        await admin.OpenAsync();
        await new NpgsqlCommand($"CREATE DATABASE \"{fixture.name}\"", admin).ExecuteNonQueryAsync();
        try
        {
            await using var db = fixture.Open();
            await ServiceSchema.InitializeAsync(db);
            return fixture;
        }
        catch { await fixture.DisposeAsync(); throw; }
    }

    public async Task<WebApplication> StartHostAsync(TimeProvider? clock = null, Action<WebApplicationBuilder>? configure = null)
    {
        using var signingKey = RSA.Create(2048);
        var app = await UserHost.BuildAsync(["--environment", "Testing"], builder =>
        {
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
            if (clock is not null)
            {
                builder.Services.AddSingleton(clock);
                // Lifecycle tests verify account-level lockout, independently of
                // the shared TestServer IP's per-minute transport limit.
                builder.Services.AddSingleton<IStartupFilter, MfaTestClientAddresses>();
            }
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:User"] = ConnectionString,
                ["Services:Key"] = "isolated-user-service-tests-key-20261012",
                ["Jwt:Issuer"] = "driver-vehicle-tests",
                ["Jwt:Audience"] = "driver-vehicle-tests",
                ["Jwt:PrivateKeyPem"] = signingKey.ExportRSAPrivateKeyPem(),
                ["Workflow:KeyDirectory"] = null,
                ["SeedDemoUser"] = "false",
                ["DevelopmentAdmin:Email"] = null
            });
        }, configure);
        await app.StartAsync();
        // Delivery scheduling is exercised explicitly against PostgreSQL in DriverWorkflowTests.
        // Stop the timer here so extracting test-only OTP fixtures cannot race provider retries.
        foreach (var worker in app.Services.GetServices<IHostedService>().OfType<WorkflowDeliveryWorker>())
            await worker.StopAsync(CancellationToken.None);
        return app;
    }

    public async ValueTask DisposeAsync()
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^smartpark_auth_test_[0-9a-f]{32}$"))
            throw new InvalidOperationException("Unsafe test database name.");
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)", admin).ExecuteNonQueryAsync();
        // Clear only this fixture's pool, without interrupting concurrent fixtures.
        using var pool = new NpgsqlConnection(ConnectionString);
        NpgsqlConnection.ClearPool(pool);
    }
}

internal sealed class MfaTestClientAddresses : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((context, handler) =>
        {
            context.Connection.RemoteIpAddress = new System.Net.IPAddress(RandomNumberGenerator.GetBytes(4));
            return handler(context);
        });
        next(app);
    };
}
