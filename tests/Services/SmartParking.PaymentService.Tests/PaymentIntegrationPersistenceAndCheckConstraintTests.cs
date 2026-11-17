using SmartParking.PaymentService.Application.Integrations;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartParking.PaymentService.Application.Common.Interfaces;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Infrastructure.Options;
using SmartParking.PaymentService.Infrastructure.Services;
using SmartParking.PaymentService.Persistence;
using SmartParking.PaymentService.Persistence.Repositories;
using Xunit;

namespace SmartParking.PaymentService.Tests;

/// <summary>
/// SPARK-370 Cluster 1: Payment persistence & schema compatibility tests.
/// Verifies that payment integration history complies with the PostgreSQL CHECK
/// constraints on policy_revisions (scope_type IN ('GLOBAL', 'TENANT', 'SITE', 'ZONE')
/// and policy_type IN ('TARIFF', 'SUBSCRIPTION', 'OVERSTAY_PENALTY', 'SPECIAL_EVENT')),
/// verifies PascalCase vs camelCase serialization/hydration round-trip,
/// and verifies that persistence failures log warnings rather than being swallowed silently.
/// </summary>
public class PaymentIntegrationPersistenceAndCheckConstraintTests
{
    private static readonly HashSet<string> AllowedPolicyTypes = new(StringComparer.Ordinal)
    {
        "TARIFF", "SUBSCRIPTION", "OVERSTAY_PENALTY", "SPECIAL_EVENT"
    };

    private static readonly HashSet<string> AllowedScopeTypes = new(StringComparer.Ordinal)
    {
        "GLOBAL", "TENANT", "SITE", "ZONE"
    };

    private static PaymentDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new PaymentDbContext(options);
    }

    [Fact]
    public async Task AppendAsync_WritesValuesConformingToPostgresCheckConstraints()
    {
        using var db = CreateInMemoryDbContext();
        var repo = new PaymentIntegrationHistoryRepository(db);
        var actorId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        const string payload = "{\"mode\":\"SANDBOX\",\"baseUrl\":\"https://api-merchant.payos.vn\"}";

        await repo.AppendAsync(PaymentIntegrationSettingsStore.PaymentIntegrationPolicyId, payload, actorId, now);

        var saved = await db.PolicyRevisions.SingleOrDefaultAsync(p =>
            p.PolicyId == PaymentIntegrationSettingsStore.PaymentIntegrationPolicyId);

        Assert.NotNull(saved);
        // PostgreSQL CHECK constraint: CHECK (scope_type IN ('GLOBAL', 'TENANT', 'SITE', 'ZONE'))
        Assert.Contains(saved.ScopeType, AllowedScopeTypes);
        Assert.Equal("GLOBAL", saved.ScopeType);
        Assert.NotEqual("PLATFORM", saved.ScopeType);

        // PostgreSQL CHECK constraint: CHECK (policy_type IN ('TARIFF', 'SUBSCRIPTION', 'OVERSTAY_PENALTY', 'SPECIAL_EVENT'))
        Assert.Contains(saved.PolicyType, AllowedPolicyTypes);
        Assert.NotEqual("PAYMENT_INTEGRATION", saved.PolicyType);

        Assert.Equal(1, saved.RevisionNumber);
        Assert.Equal(payload, saved.PolicyValuesJson);
    }

    [Fact]
    public void PostgresCheckConstraint_RejectsDisallowedScopeAndPolicyType()
    {
        // Simulate PostgreSQL CHECK constraints evaluation
        void AssertCheckConstraint(string policyType, string scopeType)
        {
            if (!AllowedPolicyTypes.Contains(policyType))
                throw new InvalidOperationException($"CHECK constraint violation on policy_type: '{policyType}' is not allowed.");
            if (!AllowedScopeTypes.Contains(scopeType))
                throw new InvalidOperationException($"CHECK constraint violation on scope_type: '{scopeType}' is not allowed.");
        }

        // Invalid values originally attempted in Round 2
        var exScope = Assert.Throws<InvalidOperationException>(() => AssertCheckConstraint("TARIFF", "PLATFORM"));
        Assert.Contains("scope_type", exScope.Message);

        var exPolicy = Assert.Throws<InvalidOperationException>(() => AssertCheckConstraint("PAYMENT_INTEGRATION", "GLOBAL"));
        Assert.Contains("policy_type", exPolicy.Message);

        // Current repository values pass
        AssertCheckConstraint(PaymentIntegrationHistoryRepository.PolicyType, PaymentIntegrationHistoryRepository.ScopeType);
    }

    [Fact]
    public async Task SerializationHydration_RoundTrip_RestoresAllFieldsAccurately()
    {
        using var db = CreateInMemoryDbContext();
        var repo = new PaymentIntegrationHistoryRepository(db);

        var services = new ServiceCollection();
        services.AddDataProtection();
        services.AddSingleton<IPaymentIntegrationHistoryRepository>(repo);
        var sp = services.BuildServiceProvider();

        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        var dataProtection = sp.GetRequiredService<IDataProtectionProvider>();

        var options = Options.Create(new PayOsOptions
        {
            Mode = "FIXTURE",
            BaseUrl = "http://localhost:5173"
        });

        var store = new PaymentIntegrationSettingsStore(options, scopeFactory, dataProtection);

        // Save a SANDBOX configuration
        store.Save(
            mode: "SANDBOX",
            baseUrl: "https://api-merchant.payos.vn",
            clientId: "test_client_id_123",
            apiKey: "test_api_key_456",
            checksumKey: "test_checksum_key_789"
        );

        // Create a new store instance pointing to the same repository (simulating service restart)
        var newStore = new PaymentIntegrationSettingsStore(options, scopeFactory, dataProtection);
        await newStore.HydrateAsync();

        var restored = newStore.GetCurrent();
        Assert.Equal("SANDBOX", restored.Mode);
        Assert.Equal("https://api-merchant.payos.vn", restored.BaseUrl);
        Assert.Equal("test_client_id_123", restored.ClientId);
        Assert.Equal("test_api_key_456", restored.ApiKey);
        Assert.Equal("test_checksum_key_789", restored.ChecksumKey);
        Assert.True(restored.IsConfigured);

        // Verify trusted webhook key is also restored
        var trustedKey = newStore.GetTrustedWebhookChecksumKey(DateTimeOffset.UtcNow);
        Assert.Equal("test_checksum_key_789", trustedKey);
    }

    [Fact]
    public async Task HydrateAsync_HandlesHistoricalPascalCaseJsonPayload()
    {
        using var db = CreateInMemoryDbContext();
        var repo = new PaymentIntegrationHistoryRepository(db);

        var services = new ServiceCollection();
        services.AddDataProtection();
        var sp = services.BuildServiceProvider();
        var dataProtection = sp.GetRequiredService<IDataProtectionProvider>();
        var protector = dataProtection.CreateProtector("SmartParking.PaymentIntegration.Credentials.v1");

        var credsJson = JsonSerializer.Serialize(new
        {
            ClientId = "legacy_client",
            ApiKey = "legacy_api_key",
            ChecksumKey = "legacy_checksum",
            PreviousChecksumKey = "legacy_prev_checksum"
        });
        var protectedCreds = protector.Protect(credsJson);

        // Historical PascalCase payload without camelCase naming policy
        var pascalPayload = JsonSerializer.Serialize(new
        {
            Mode = "LIVE",
            BaseUrl = "https://api-merchant.payos.vn",
            CredentialsProtected = protectedCreds
        });

        await repo.AppendAsync(
            PaymentIntegrationSettingsStore.PaymentIntegrationPolicyId,
            pascalPayload,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        services.AddSingleton<IPaymentIntegrationHistoryRepository>(repo);
        var spWithRepo = services.BuildServiceProvider();
        var scopeFactory = spWithRepo.GetRequiredService<IServiceScopeFactory>();

        var options = Options.Create(new PayOsOptions());
        var store = new PaymentIntegrationSettingsStore(options, scopeFactory, dataProtection);

        await store.HydrateAsync();

        var config = store.GetCurrent();
        Assert.Equal("LIVE", config.Mode);
        Assert.Equal("https://api-merchant.payos.vn", config.BaseUrl);
        Assert.Equal("legacy_client", config.ClientId);
        Assert.Equal("legacy_api_key", config.ApiKey);
        Assert.Equal("legacy_checksum", config.ChecksumKey);
    }

    [Fact]
    public void PersistenceFailure_ThrowsAndRollsBackInMemoryState()
    {
        var services = new ServiceCollection();
        services.AddDataProtection();

        var failingRepo = new FailingHistoryRepository();
        services.AddSingleton<IPaymentIntegrationHistoryRepository>(failingRepo);
        var sp = services.BuildServiceProvider();

        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        var dataProtection = sp.GetRequiredService<IDataProtectionProvider>();

        var logger = new TestLogger<PaymentIntegrationSettingsStore>();
        var options = Options.Create(new PayOsOptions { Mode = PaymentIntegrationSettingsStore.SandboxMode });

        var store = new PaymentIntegrationSettingsStore(options, scopeFactory, dataProtection, logger);

        // A failed write must not be reported as a successful save, and the in-memory
        // change must be rolled back so the caller can retry safely.
        var ex = Assert.Throws<PaymentIntegrationPersistenceException>(() =>
            store.Save("LIVE", "https://api-merchant.payos.vn", "client", "key", "checksum"));
        Assert.Contains("could not be persisted", ex.Message);

        Assert.Equal(PaymentIntegrationSettingsStore.SandboxMode, store.GetCurrent().Mode);
        Assert.Equal(string.Empty, store.ForMode(PaymentIntegrationSettingsStore.LiveMode).ClientId);

        // The failure was logged for observability.
        var error = Assert.Single(logger.Logs, l => l.LogLevel == LogLevel.Error);
        Assert.Contains("persistence failed", error.Message);
        Assert.Contains(nameof(DbUpdateException), error.Message);
        Assert.Null(error.Exception); // Provider exception text can contain reflected credentials.
    }

    private sealed class FailingHistoryRepository : IPaymentIntegrationHistoryRepository
    {
        public Task<PaymentIntegrationRevision?> GetLatestAsync(Guid policyId, CancellationToken cancellationToken = default)
            => Task.FromResult<PaymentIntegrationRevision?>(null);

        public Task AppendAsync(Guid policyId, string policyValuesJson, Guid createdBy, DateTimeOffset createdAt, CancellationToken cancellationToken = default)
            => throw new DbUpdateException("Simulated PostgreSQL connection failure or constraint violation");
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<(LogLevel LogLevel, string Message, Exception? Exception)> Logs { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Logs.Add((logLevel, formatter(state, exception), exception));
        }
    }
}
