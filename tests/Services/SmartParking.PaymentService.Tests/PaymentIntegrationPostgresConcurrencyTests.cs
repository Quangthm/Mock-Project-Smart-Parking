using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartParking.PaymentService.Application.Integrations;
using Npgsql;
using SmartParking.PaymentService.Infrastructure.Adapters.Outbound;
using SmartParking.PaymentService.Infrastructure.Options;
using SmartParking.PaymentService.Infrastructure.Services;
using SmartParking.PaymentService.Persistence;
using SmartParking.PaymentService.Persistence.Repositories;
using Xunit;

namespace SmartParking.PaymentService.Tests;

/// <summary>
/// Runs only when an isolated PostgreSQL payment server is available. The fixture creates a brand
/// new generated database, applies the payment schema, and drops only that generated database; the
/// running application databases are never touched. Set
/// <c>SMARTPARK_PAYMENT_TEST_CONNECTION</c> (and, when it carries no password, the shared
/// <c>SMARTPARK_DEV_PASSWORD</c>) to enable it.
/// </summary>
public sealed class PaymentPostgresFactAttribute : FactAttribute
{
    public PaymentPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SMARTPARK_PAYMENT_TEST_CONNECTION")))
        {
            Skip = "Set SMARTPARK_PAYMENT_TEST_CONNECTION to an isolated PostgreSQL server " +
                   "(with CREATE DATABASE permission) to run the concurrent revision-allocation proof.";
        }
    }
}

/// <summary>
/// SPARK-370 P1: revision allocation must be atomic across competing Appends. Two or more
/// connections computing <c>MAX(RevisionNumber)+1</c> without serialization would collide on the
/// existing <c>(policy_id, revision_number)</c> unique index. The repository serializes allocation
/// with a transaction-scoped PostgreSQL advisory lock on the deterministic policy id, so every
/// writer observes a distinct, contiguous revision number.
/// </summary>
public class PaymentIntegrationPostgresConcurrencyTests
{
    [PaymentPostgresFact, Trait("Category", "Postgres")]
    public async Task CheckoutBinding_IsCommittedAndReadableByIndependentConnectionBeforeOutbound()
    {
        await using var fixture = await PaymentTempDatabase.CreateAsync();
        var protection = new EphemeralDataProtectionProvider();
        var options = Options.Create(new PayOsOptions
        {
            Mode = "LIVE", LiveClientId = "synthetic-client", LiveApiKey = "synthetic-api",
            LiveChecksumKey = "synthetic-commit-checksum"
        });
        var settings = new PaymentIntegrationSettingsStore(options);
        await using var writer = fixture.Open();
        var bindings = new CheckoutSigningBindings(new CheckoutBindingRepository(writer), protection);
        using var handler = new BindingVisibilityHandler(async () =>
        {
            await using var reader = fixture.Open();
            var recovered = new CheckoutSigningBindings(new CheckoutBindingRepository(reader), protection);
            Assert.Equal("synthetic-commit-checksum", await recovered.ReadChecksumKeyAsync(741852965, default));
        });
        using var client = new HttpClient(handler);
        var adapter = new PayOsAdapter(client, options, NullLogger<PayOsAdapter>.Instance,
            settings, bindings);

        await adapter.InitiatePaymentAsync(new(Guid.NewGuid(), 741852965, 25000, "commit ordering",
            "https://example.invalid/return", "https://example.invalid/cancel", DateTimeOffset.UtcNow.AddMinutes(15)));

        Assert.Equal(1, handler.Calls);
        Assert.True(handler.BindingWasVisible);
    }

    private sealed class BindingVisibilityHandler(Func<Task> verify) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public bool BindingWasVisible { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            await verify();
            BindingWasVisible = true;
            return new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable);
        }
    }

    [PaymentPostgresFact, Trait("Category", "Postgres")]
    public async Task CheckoutBinding_SurvivesFreshContextAndKeyProvider_AndCannotBeReplaced()
    {
        await using var fixture = await PaymentTempDatabase.CreateAsync();
        var keyDirectory = Directory.CreateTempSubdirectory("smartpark-binding-");
        ServiceProvider Provider()
        {
            var services = new ServiceCollection();
            services.AddDataProtection().SetApplicationName("SmartParking.PaymentService")
                .PersistKeysToFileSystem(keyDirectory);
            return services.BuildServiceProvider();
        }
        const long orderCode = 741852963;
        await using var initial = fixture.Open();
        var initialCount = await initial.PolicyRevisions.CountAsync();
        var issued = new PaymentConfigurationSnapshot(7, "SANDBOX", "https://api-merchant.payos.vn",
            "synthetic-client", "synthetic-api", "synthetic-original-checksum");
        try
        {
            using (var provider = Provider())
            await using (var db = fixture.Open())
            {
                var binding = new CheckoutSigningBindings(new CheckoutBindingRepository(db),
                    provider.GetRequiredService<IDataProtectionProvider>());
                Assert.True(await binding.BindAsync(orderCode, issued, default));
                Assert.True(await binding.BindAsync(orderCode, issued, default));
                var protectedValue = await new CheckoutBindingRepository(db).ReadAsync(orderCode, default);
                Assert.NotNull(protectedValue);
                Assert.DoesNotContain(issued.ChecksumKey!, protectedValue);
            }
            using (var provider = Provider())
            await using (var db = fixture.Open())
            {
                var binding = new CheckoutSigningBindings(new CheckoutBindingRepository(db),
                    provider.GetRequiredService<IDataProtectionProvider>());
                Assert.Equal(issued.ChecksumKey, await binding.ReadChecksumKeyAsync(orderCode, default));
                Assert.False(await binding.BindAsync(orderCode, issued with { Mode = "LIVE", Version = 8,
                    ChecksumKey = "synthetic-rotated-checksum" }, default));
                Assert.Equal(issued.ChecksumKey, await binding.ReadChecksumKeyAsync(orderCode, default));
                Assert.Null(await binding.ReadChecksumKeyAsync(orderCode + 1, default));
                Assert.Equal(initialCount + 1, await db.PolicyRevisions.CountAsync());
            }
        }
        finally
        {
            keyDirectory.Delete(recursive: true);
        }
    }

    [PaymentPostgresFact, Trait("Category", "Postgres")]
    public async Task ConcurrentAppends_AllocateUniqueContiguousRevisionNumbers()
    {
        await using var fixture = await PaymentTempDatabase.CreateAsync();

        const int writers = 12;
        await RunConcurrentAppendsAsync(fixture, writers);

        var revisions = await ReadRevisionNumbersAsync(fixture);

        Assert.Equal(writers, revisions.Count);
        Assert.Equal(writers, revisions.Distinct().Count());
        Assert.Equal(Enumerable.Range(1, writers), revisions.OrderBy(r => r));
    }

    [PaymentPostgresFact, Trait("Category", "Postgres")]
    public async Task ConcurrentAppends_AcrossTwoWaves_ContinueFromPreviousMaximum()
    {
        await using var fixture = await PaymentTempDatabase.CreateAsync();

        const int perWave = 8;
        await RunConcurrentAppendsAsync(fixture, perWave);
        await RunConcurrentAppendsAsync(fixture, perWave);

        var revisions = await ReadRevisionNumbersAsync(fixture);

        Assert.Equal(perWave * 2, revisions.Count);
        Assert.Equal(perWave * 2, revisions.Distinct().Count());
        Assert.Equal(Enumerable.Range(1, perWave * 2), revisions.OrderBy(r => r));
    }

    private static async Task RunConcurrentAppendsAsync(PaymentTempDatabase fixture, int writers)
    {
        var tasks = Enumerable.Range(0, writers).Select(_ => Task.Run(async () =>
        {
            await using var db = fixture.Open();
            var repository = new PaymentIntegrationHistoryRepository(db);
            await repository.AppendAsync(
                PaymentIntegrationSettingsStore.PaymentIntegrationPolicyId,
                "{\"mode\":\"SANDBOX\"}",
                Guid.NewGuid(),
                DateTimeOffset.UtcNow);
        }));

        await Task.WhenAll(tasks);
    }

    private static async Task<List<int>> ReadRevisionNumbersAsync(PaymentTempDatabase fixture)
    {
        await using var verification = fixture.Open();
        return await verification.PolicyRevisions
            .Where(p => p.PolicyId == PaymentIntegrationSettingsStore.PaymentIntegrationPolicyId)
            .Select(p => p.RevisionNumber)
            .ToListAsync();
    }

    private static PaymentDbContext Open(string connectionString) =>
        new(new DbContextOptionsBuilder<PaymentDbContext>().UseNpgsql(connectionString).Options);

    private sealed class PaymentTempDatabase : IAsyncDisposable
    {
        private const string GeneratedPrefix = "smartpark_payment_test_";

        private readonly string _adminConnection;
        private readonly string _name = GeneratedPrefix + Guid.NewGuid().ToString("N");

        public string ConnectionString { get; }

        private PaymentTempDatabase(NpgsqlConnectionStringBuilder admin)
        {
            _adminConnection = admin.ConnectionString;
            ConnectionString = new NpgsqlConnectionStringBuilder(admin.ConnectionString)
            {
                Database = _name,
            }.ConnectionString;
        }

        public PaymentDbContext Open() => PaymentIntegrationPostgresConcurrencyTests.Open(ConnectionString);

        public static async Task<PaymentTempDatabase> CreateAsync()
        {
            var fixture = new PaymentTempDatabase(ResolveAdminBuilder());

            await using var admin = new NpgsqlConnection(fixture._adminConnection);
            await admin.OpenAsync();
            await new NpgsqlCommand($"CREATE DATABASE \"{fixture._name}\"", admin).ExecuteNonQueryAsync();
            try
            {
                var root = FindRepositoryRoot();
                var schema = await File.ReadAllTextAsync(Path.Combine(root,
                    "scripts", "database", "microservices", "04-payment-pricing-service-db.sql"));
                await using var db = fixture.Open();

                // Applied through a raw command: the payment seed contains jsonb '{...}' literals
                // that EF's ExecuteSqlRawAsync would misinterpret as composite-format placeholders.
                var connection = db.Database.GetDbConnection();
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = schema;
                await command.ExecuteNonQueryAsync();
                return fixture;
            }
            catch
            {
                await fixture.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            // Only the generated database is ever dropped; existence and name are asserted first.
            Assert.Matches("^smartpark_payment_test_[0-9a-f]{32}$", _name);
            using (var pool = new NpgsqlConnection(ConnectionString))
                NpgsqlConnection.ClearPool(pool);

            await using var admin = new NpgsqlConnection(_adminConnection);
            await admin.OpenAsync();
            await new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_name}\" WITH (FORCE)", admin)
                .ExecuteNonQueryAsync();
        }

        private static NpgsqlConnectionStringBuilder ResolveAdminBuilder()
        {
            var raw = Environment.GetEnvironmentVariable("SMARTPARK_PAYMENT_TEST_CONNECTION")
                ?? throw new InvalidOperationException("Configure the isolated PostgreSQL test server.");

            var builder = raw.Contains('=', StringComparison.Ordinal)
                ? new NpgsqlConnectionStringBuilder(raw)
                : new NpgsqlConnectionStringBuilder();

            if (string.IsNullOrWhiteSpace(builder.Host)) builder.Host = "127.0.0.1";
            if (!raw.Contains("port", StringComparison.OrdinalIgnoreCase)) builder.Port = 5444;
            if (string.IsNullOrWhiteSpace(builder.Username)) builder.Username = "smartpark_payment";
            if (string.IsNullOrWhiteSpace(builder.Database)) builder.Database = "smartpark_payment";
            if (string.IsNullOrWhiteSpace(builder.Password)) builder.Password = ResolveDevPassword();
            return builder;
        }

        private static string ResolveDevPassword()
        {
            var fromEnvironment = Environment.GetEnvironmentVariable("SMARTPARK_DEV_PASSWORD");
            if (!string.IsNullOrWhiteSpace(fromEnvironment)) return fromEnvironment;

            var envPath = Path.Combine(FindRepositoryRoot(), ".env");
            if (File.Exists(envPath))
            {
                foreach (var line in File.ReadAllLines(envPath))
                {
                    var trimmed = line.Trim();
                    if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;
                    var separator = trimmed.IndexOf('=');
                    if (separator <= 0) continue;
                    if (!string.Equals(trimmed[..separator].Trim(), "SMARTPARK_DEV_PASSWORD", StringComparison.Ordinal))
                        continue;
                    var value = trimmed[(separator + 1)..].Trim().Trim('"');
                    if (!string.IsNullOrWhiteSpace(value)) return value;
                }
            }

            throw new InvalidOperationException(
                "The test connection carries no password and SMARTPARK_DEV_PASSWORD is not set in the environment or .env.");
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
    }
}
