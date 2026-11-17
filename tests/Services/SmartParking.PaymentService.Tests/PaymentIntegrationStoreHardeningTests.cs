using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartParking.PaymentService.Application.Common.Interfaces;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Application.Integrations;
using SmartParking.PaymentService.Infrastructure.Adapters.Outbound;
using SmartParking.PaymentService.Infrastructure.Options;
using SmartParking.PaymentService.Infrastructure.Services;
using SmartParking.PaymentService.Persistence;
using SmartParking.PaymentService.Persistence.Repositories;
using Xunit;

namespace SmartParking.PaymentService.Tests;

/// <summary>
/// SPARK-370 P4 hardening regressions:
/// 1. checkout-time mode/key binding for webhook verification,
/// 2. multi-mode credential + historical keyring restore across a new store instance,
/// 3. atomic revision allocation under concurrent saves,
/// 4. honest persistence + hydration retry,
/// 5. snapshot-aware cancellation redaction,
/// 6. DataProtection key reuse across provider/container recreation.
/// </summary>
public class PaymentIntegrationStoreHardeningTests
{
    private const string LiveBaseUrl = "https://api-merchant.payos.vn";
    private const string FixtureBaseUrl = "http://localhost:5173";

    private static PaymentDbContext CreateInMemoryDbContext(string? databaseName = null) =>
        new(new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options);

    private static IDataProtectionProvider BuildDataProtection(string? keysDirectory = null)
    {
        var services = new ServiceCollection();
        var builder = services.AddDataProtection().SetApplicationName("SmartParking.PaymentService");
        if (!string.IsNullOrWhiteSpace(keysDirectory))
        {
            builder.PersistKeysToFileSystem(new DirectoryInfo(keysDirectory));
        }

        return services.BuildServiceProvider().GetRequiredService<IDataProtectionProvider>();
    }

    private static PaymentIntegrationSettingsStore CreateStore(
        IPaymentIntegrationHistoryRepository repository,
        IDataProtectionProvider dataProtection,
        PayOsOptions? options = null,
        ILogger<PaymentIntegrationSettingsStore>? logger = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(repository);
        var provider = services.BuildServiceProvider();

        return new PaymentIntegrationSettingsStore(
            Options.Create(options ?? new PayOsOptions()),
            provider.GetRequiredService<IServiceScopeFactory>(),
            dataProtection,
            logger);
    }

    private static PaymentIntegrationSettingsStore CreateStore(
        PaymentDbContext dbContext,
        IDataProtectionProvider dataProtection,
        PayOsOptions? options = null,
        ILogger<PaymentIntegrationSettingsStore>? logger = null) =>
        CreateStore(new PaymentIntegrationHistoryRepository(dbContext), dataProtection, options, logger);

    private static (string Json, string Signature) BuildWebhook(long orderCode, long amount, string checksumKey)
    {
        var data = new Dictionary<string, object?>
        {
            ["amount"] = amount,
            ["orderCode"] = orderCode,
            ["reference"] = "REF_" + orderCode,
            ["paymentLinkId"] = "LINK_" + orderCode,
        };

        var signature = PayOsSignatureService.CreateSignatureFromDictionary(data, checksumKey);
        var json = $$"""
        {
            "code": "00",
            "desc": "success",
            "data": {
                "orderCode": {{orderCode}},
                "amount": {{amount}},
                "reference": "REF_{{orderCode}}",
                "paymentLinkId": "LINK_{{orderCode}}"
            },
            "signature": "{{signature}}"
        }
        """;

        return (json, signature);
    }

    // ---------------------------------------------------------------------
    // Fix 1: checkout-time mode + key version binding.
    // ---------------------------------------------------------------------

    private sealed class AlwaysSuccessHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"code\":\"00\",\"desc\":\"success\",\"data\":{\"checkoutUrl\":\"https://pay.payos.vn/web/x\",\"paymentLinkId\":\"LINK-OK\",\"qrCode\":\"QR\",\"accountNumber\":\"1\",\"accountName\":\"N\",\"bin\":\"970422\"}}",
                    Encoding.UTF8,
                    "application/json")
            });
    }

    [Fact]
    public async Task Webhook_UsesKeyBoundAtCheckout_DespiteLaterRotationAndCreationTime()
    {
        const string checkoutKey = "checkout-key-version-one";
        const string rotatedKey = "rotated-key-version-two";
        const long orderCode = 424242L;

        var store = new PaymentIntegrationSettingsStore(Options.Create(new PayOsOptions
        {
            Mode = PaymentIntegrationSettingsStore.LiveMode,
            LiveClientId = "live-client",
            LiveApiKey = "live-api",
            LiveChecksumKey = checkoutKey,
        }));

        var adapter = new PayOsAdapter(
            new HttpClient(new AlwaysSuccessHandler()),
            Options.Create(new PayOsOptions()),
            new NullTestLogger<PayOsAdapter>(),
            store, CheckoutBindingFixture.Create());

        var initiated = await adapter.InitiatePaymentAsync(new PaymentInitiateRequest(
            TransactionId: Guid.NewGuid(),
            OrderCode: orderCode,
            AmountVnd: 50000,
            Description: "parking",
            ReturnUrl: "https://example.com/return",
            CancelUrl: "https://example.com/cancel",
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(15)));
        Assert.True(initiated.IsSuccess);

        // Rotate after checkout creation.
        store.Save(PaymentIntegrationSettingsStore.LiveMode, LiveBaseUrl, null, null, rotatedKey);

        // A creation-time lookup with a post-rotation timestamp would select the rotated key.
        var postRotationTimestamp = DateTimeOffset.UtcNow.AddMinutes(1);

        var (checkoutJson, checkoutSignature) = BuildWebhook(orderCode, 50000, checkoutKey);
        var accepted = await adapter.VerifyAndParseWebhookAsync(checkoutJson, checkoutSignature, postRotationTimestamp);
        Assert.True(accepted.IsValidSignature);
        Assert.Equal("SUCCESS", accepted.PaymentStatus);

        // The rotated key must not verify this order; the bound key is used exclusively.
        var (rotatedJson, rotatedSignature) = BuildWebhook(orderCode, 50000, rotatedKey);
        var rejected = await adapter.VerifyAndParseWebhookAsync(rotatedJson, rotatedSignature, postRotationTimestamp);
        Assert.False(rejected.IsValidSignature);
        Assert.Equal("FAILED", rejected.PaymentStatus);
    }

    [Fact]
    public async Task Webhook_UsesModeBoundAtCheckout_AfterModeSwitch()
    {
        const string sandboxKey = "sandbox-checkout-key";
        const long orderCode = 535353L;

        var store = new PaymentIntegrationSettingsStore(Options.Create(new PayOsOptions
        {
            Mode = PaymentIntegrationSettingsStore.SandboxMode,
            SandboxClientId = "sandbox-client",
            SandboxApiKey = "sandbox-api",
            SandboxChecksumKey = sandboxKey,
        }));

        var adapter = new PayOsAdapter(
            new HttpClient(new AlwaysSuccessHandler()),
            Options.Create(new PayOsOptions()),
            new NullTestLogger<PayOsAdapter>(),
            store, CheckoutBindingFixture.Create());

        var initiated = await adapter.InitiatePaymentAsync(new PaymentInitiateRequest(
            TransactionId: Guid.NewGuid(),
            OrderCode: orderCode,
            AmountVnd: 50000,
            Description: "parking",
            ReturnUrl: "https://example.com/return",
            CancelUrl: "https://example.com/cancel",
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(15)));
        Assert.True(initiated.IsSuccess);

        // Admin switches to LIVE with a different key before the callback arrives.
        store.Save(PaymentIntegrationSettingsStore.LiveMode, LiveBaseUrl, "live-client", "live-api", "live-key");

        var (json, signature) = BuildWebhook(orderCode, 50000, sandboxKey);
        var accepted = await adapter.VerifyAndParseWebhookAsync(
            json, signature, DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.True(accepted.IsValidSignature);
    }

    // ---------------------------------------------------------------------
    // Fix 2: multi-mode credentials + historical keyrings survive a restart.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task NewStoreInstance_RestoresAllModesAndHistoricalKeyrings()
    {
        const string oldKey = "live-old-key-1111";
        const string newKey = "live-new-key-2222";

        using var db = CreateInMemoryDbContext();
        var dataProtection = BuildDataProtection();
        var first = CreateStore(db, dataProtection, new PayOsOptions
        {
            Mode = PaymentIntegrationSettingsStore.FixtureMode,
        });

        first.Save(PaymentIntegrationSettingsStore.LiveMode, LiveBaseUrl, "live-client", "live-api", oldKey);
        // Capture the order-creation instant while the first key is still active, then force a
        // measurable gap so the rotation timestamp is strictly later than that instant. Without
        // the gap the wall clock can return the same tick for both saves, which would make the
        // rotated key appear active for an order created "before" the rotation.
        var orderCreatedBeforeRotation = DateTimeOffset.UtcNow;
        Thread.Sleep(100);
        first.Save(PaymentIntegrationSettingsStore.LiveMode, LiveBaseUrl, null, null, newKey);
        first.Save(PaymentIntegrationSettingsStore.FixtureMode, FixtureBaseUrl, null, null, null);

        // Simulate restart/container recreation: new store over the same persisted history.
        var restarted = CreateStore(db, dataProtection, new PayOsOptions());
        await restarted.HydrateAsync();

        Assert.Equal(PaymentIntegrationSettingsStore.FixtureMode, restarted.GetCurrent().Mode);

        var restoredLive = restarted.ForMode(PaymentIntegrationSettingsStore.LiveMode);
        Assert.Equal("live-client", restoredLive.ClientId);
        Assert.Equal("live-api", restoredLive.ApiKey);
        Assert.Equal(newKey, restoredLive.ChecksumKey);
        Assert.Equal(oldKey, restoredLive.PreviousChecksumKey);

        // Both historical and current keys are available for redaction/verification.
        var sensitive = restarted.GetSensitiveValues();
        Assert.Contains(oldKey, sensitive);
        Assert.Contains(newKey, sensitive);

        // The retired key is still trusted for an order created before the rotation.
        Assert.Equal(oldKey, restarted.GetTrustedWebhookChecksumKey(orderCreatedBeforeRotation));

        // Switching back to LIVE with omitted credentials keeps the restored LIVE credentials.
        restarted.Save(PaymentIntegrationSettingsStore.LiveMode, LiveBaseUrl, null, null, null);
        var liveAfterSwitch = restarted.ForMode(PaymentIntegrationSettingsStore.LiveMode);
        Assert.Equal("live-client", liveAfterSwitch.ClientId);
        Assert.Equal("live-api", liveAfterSwitch.ApiKey);
        Assert.Equal(newKey, liveAfterSwitch.ChecksumKey);
    }

    // ---------------------------------------------------------------------
    // Fix 3: concurrent append never duplicates (PolicyId, RevisionNumber).
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ConcurrentAppends_AllocateUniqueRevisionNumbers()
    {
        var databaseName = Guid.NewGuid().ToString();
        var dbOptions = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        const int writers = 16;
        var tasks = Enumerable.Range(0, writers).Select(_ => Task.Run(async () =>
        {
            using var context = new PaymentDbContext(dbOptions);
            var repository = new PaymentIntegrationHistoryRepository(context);
            await repository.AppendAsync(
                PaymentIntegrationSettingsStore.PaymentIntegrationPolicyId,
                "{\"mode\":\"SANDBOX\"}",
                Guid.NewGuid(),
                DateTimeOffset.UtcNow);
        }));

        await Task.WhenAll(tasks);

        using var verification = new PaymentDbContext(dbOptions);
        var revisions = await verification.PolicyRevisions
            .Where(p => p.PolicyId == PaymentIntegrationSettingsStore.PaymentIntegrationPolicyId)
            .Select(p => p.RevisionNumber)
            .ToListAsync();

        Assert.Equal(writers, revisions.Count);
        Assert.Equal(writers, revisions.Distinct().Count());
        Assert.Equal(Enumerable.Range(1, writers), revisions.OrderBy(r => r));
    }

    // ---------------------------------------------------------------------
    // Fix 4: failed persistence rolls back; hydration retries after failure.
    // ---------------------------------------------------------------------

    private sealed class FlakyHistoryRepository : IPaymentIntegrationHistoryRepository
    {
        private readonly IPaymentIntegrationHistoryRepository _inner;
        private int _writeFailuresLeft;
        private int _readFailuresLeft;

        public FlakyHistoryRepository(
            IPaymentIntegrationHistoryRepository inner,
            int writeFailures,
            int readFailures = 0)
        {
            _inner = inner;
            _writeFailuresLeft = writeFailures;
            _readFailuresLeft = readFailures;
        }

        public Task<PaymentIntegrationRevision?> GetLatestAsync(Guid policyId, CancellationToken cancellationToken = default)
        {
            if (_readFailuresLeft > 0)
            {
                _readFailuresLeft--;
                throw new InvalidOperationException("Transient database outage (read).");
            }

            return _inner.GetLatestAsync(policyId, cancellationToken);
        }

        public Task AppendAsync(
            Guid policyId,
            string policyValuesJson,
            Guid createdBy,
            DateTimeOffset createdAt,
            CancellationToken cancellationToken = default)
        {
            if (_writeFailuresLeft > 0)
            {
                _writeFailuresLeft--;
                throw new InvalidOperationException("Transient database outage (write).");
            }

            return _inner.AppendAsync(policyId, policyValuesJson, createdBy, createdAt, cancellationToken);
        }
    }

    [Fact]
    public void FailedSave_ThrowsAndCanBeRetriedWithoutDivergedState()
    {
        using var db = CreateInMemoryDbContext();
        var dataProtection = BuildDataProtection();
        var repository = new FlakyHistoryRepository(
            new PaymentIntegrationHistoryRepository(db),
            writeFailures: 1);
        var store = CreateStore(repository, dataProtection, new PayOsOptions
        {
            Mode = PaymentIntegrationSettingsStore.SandboxMode,
        });

        Assert.Throws<PaymentIntegrationPersistenceException>(() =>
            store.Save(PaymentIntegrationSettingsStore.LiveMode, LiveBaseUrl, "live-client", "live-api", "live-key"));

        // Rolled back: the failed save must not be observable.
        Assert.Equal(PaymentIntegrationSettingsStore.SandboxMode, store.GetCurrent().Mode);
        Assert.Equal(string.Empty, store.ForMode(PaymentIntegrationSettingsStore.LiveMode).ClientId);

        // Retry succeeds and is now durable.
        store.Save(PaymentIntegrationSettingsStore.LiveMode, LiveBaseUrl, "live-client", "live-api", "live-key");
        Assert.Equal(PaymentIntegrationSettingsStore.LiveMode, store.GetCurrent().Mode);
        Assert.Equal("live-client", store.GetCurrent().ClientId);
    }

    [Fact]
    public void Hydration_RetriesAfterTransientReadFailure()
    {
        using var db = CreateInMemoryDbContext();
        var dataProtection = BuildDataProtection();

        var writer = CreateStore(db, dataProtection, new PayOsOptions { Mode = PaymentIntegrationSettingsStore.SandboxMode });
        writer.Save(PaymentIntegrationSettingsStore.LiveMode, LiveBaseUrl, "live-client", "live-api", "live-key");

        var flaky = new FlakyHistoryRepository(new PaymentIntegrationHistoryRepository(db), writeFailures: 0, readFailures: 1);
        var restarted = CreateStore(flaky, dataProtection, new PayOsOptions { Mode = PaymentIntegrationSettingsStore.SandboxMode });

        // No payment operation may use defaults while authoritative configuration is unknown.
        Assert.Throws<PaymentIntegrationPersistenceException>(() => restarted.GetCurrent());

        // Second access: hydration retries and succeeds.
        Assert.Equal(PaymentIntegrationSettingsStore.LiveMode, restarted.GetCurrent().Mode);
        Assert.Equal("live-client", restarted.GetCurrent().ClientId);
    }

    [Fact]
    public async Task FailedHydration_SaveDoesNotAppendDefaults_AndRetryPreservesCredentials()
    {
        using var db = CreateInMemoryDbContext();
        var protector = BuildDataProtection();
        var writer = CreateStore(db, protector);
        writer.Save(PaymentIntegrationSettingsStore.LiveMode, LiveBaseUrl, "saved-client", "saved-api", "saved-key");
        var before = await db.PolicyRevisions.CountAsync();
        var restarted = CreateStore(new FlakyHistoryRepository(
            new PaymentIntegrationHistoryRepository(db), 0, 1), protector);

        Assert.Throws<PaymentIntegrationPersistenceException>(() => restarted.Save(
            PaymentIntegrationSettingsStore.LiveMode, LiveBaseUrl, null, null, null));
        Assert.Equal(before, await db.PolicyRevisions.CountAsync());

        restarted.Save(PaymentIntegrationSettingsStore.LiveMode, LiveBaseUrl, null, null, null);
        var restored = CreateStore(db, protector).GetCurrent();
        Assert.Equal("saved-client", restored.ClientId);
        Assert.Equal("saved-api", restored.ApiKey);
        Assert.Equal("saved-key", restored.ChecksumKey);
    }

    private sealed class BlockingFailedAppend : IPaymentIntegrationHistoryRepository
    {
        private readonly IPaymentIntegrationHistoryRepository _inner;
        private int _appends;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public BlockingFailedAppend(IPaymentIntegrationHistoryRepository inner) => _inner = inner;

        public Task<PaymentIntegrationRevision?> GetLatestAsync(Guid id, CancellationToken ct = default) =>
            _inner.GetLatestAsync(id, ct);

        public async Task AppendAsync(Guid id, string json, Guid actor, DateTimeOffset at, CancellationToken ct = default)
        {
            if (Interlocked.Increment(ref _appends) == 1)
            {
                Entered.SetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
                throw new InvalidOperationException("First append failed.");
            }
            await _inner.AppendAsync(id, json, actor, at, ct);
        }
    }

    [Fact]
    public async Task ConcurrentSave_FailedAppendCannotRollbackAnotherSaveOrPublishItsSecrets()
    {
        using var db = CreateInMemoryDbContext();
        var protector = BuildDataProtection();
        var repository = new BlockingFailedAppend(new PaymentIntegrationHistoryRepository(db));
        var store = CreateStore(repository, protector);
        var first = Task.Run(() => Assert.Throws<PaymentIntegrationPersistenceException>(() =>
            store.Save(PaymentIntegrationSettingsStore.LiveMode, LiveBaseUrl, "uncommitted", "first-api", "first-key")));
        await repository.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var second = Task.Run(() => store.Save(PaymentIntegrationSettingsStore.SandboxMode,
            LiveBaseUrl, "accepted", "second-api", "second-key"));
        var reader = Task.Run(() => store.GetCurrent());
        try
        {
            await Task.Delay(50);
            Assert.False(second.IsCompleted);
            Assert.False(reader.IsCompleted);
        }
        finally
        {
            repository.Release.TrySetResult();
        }
        await first;
        await second;
        Assert.NotEqual("uncommitted", (await reader).ClientId);
        Assert.Equal("accepted", store.GetCurrent().ClientId);
        var restarted = CreateStore(db, protector);
        Assert.Equal("accepted", restarted.GetCurrent().ClientId);
        Assert.Equal(string.Empty, restarted.ForMode(PaymentIntegrationSettingsStore.LiveMode).ClientId);
    }

    // ---------------------------------------------------------------------
    // Fix 5: cancellation redaction uses the in-flight snapshot.
    // ---------------------------------------------------------------------

    private sealed class StubIntegrationSettings : IPaymentIntegrationSettings
    {
        public StubIntegrationSettings(PaymentConfigurationSnapshot snapshot) => Snapshot = snapshot;

        public PaymentConfigurationSnapshot Snapshot { get; }

        public PaymentIntegrationConfiguration GetCurrent() => new(
            Snapshot.Mode, Snapshot.BaseUrl, Snapshot.ClientId, Snapshot.ApiKey, Snapshot.ChecksumKey,
            null, true, PaymentIntegrationSettingsStore.StatusUntested, null, Snapshot.Version);

        public PaymentConfigurationSnapshot GetCurrentSnapshot() => Snapshot;

        public PaymentIntegrationConfiguration ForMode(string mode) => GetCurrent();

        public PaymentIntegrationConfiguration Save(string mode, string baseUrl, string? clientId, string? apiKey, string? checksumKey) => GetCurrent();

        public IReadOnlyList<string> AllowedBaseUrls(string mode) => new[] { LiveBaseUrl };

        public string? GetTrustedWebhookChecksumKey(DateTimeOffset? orderCreatedAt) => Snapshot.ChecksumKey;

        // Deliberately empty: only the in-flight snapshot can provide the echoed secrets.
        public IReadOnlyCollection<string> GetSensitiveValues() => Array.Empty<string>();

        public void MarkProbeResult(int configVersion, bool success)
        {
        }

        public long NextConfigVersion() => Snapshot.Version + 1;
    }

    [Fact]
    public async Task CancellationError_EchoedSnapshotCredentials_AreRedacted()
    {
        const string clientId = "snapshot-client-id-echo";
        const string apiKey = "snapshot-api-key-echo";
        const string checksumKey = "snapshot-checksum-key-echo";

        var snapshot = new PaymentConfigurationSnapshot(
            Version: 7,
            Mode: PaymentIntegrationSettingsStore.LiveMode,
            BaseUrl: LiveBaseUrl,
            ClientId: clientId,
            ApiKey: apiKey,
            ChecksumKey: checksumKey);

        var echoed = "{\"code\":\"01\",\"desc\":\"rejected " + clientId + " / " + apiKey + " / " + checksumKey +
                     "\",\"data\":{\"status\":\"PENDING\"}}";
        var handler = new StaticResponseHandler(echoed);
        var logger = new NullTestLogger<PayOsAdapter>();
        var adapter = new PayOsAdapter(
            new HttpClient(handler),
            Options.Create(new PayOsOptions()),
            logger,
            new StubIntegrationSettings(snapshot));

        var result = await adapter.CancelPaymentAsync(123L, Guid.NewGuid(), "LINK-123");

        Assert.False(result.IsSuccess);
        var emitted = string.Join("\n", logger.Messages);
        Assert.Contains("[REDACTED]", emitted);
        Assert.DoesNotContain(clientId, emitted);
        Assert.DoesNotContain(apiKey, emitted);
        Assert.DoesNotContain(checksumKey, emitted);
    }

    private sealed class StaticResponseHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
    }

    // ---------------------------------------------------------------------
    // Fix 6: DataProtection key persistence across provider recreation.
    // ---------------------------------------------------------------------

    [Fact]
    public void NewDataProtectionProvider_WithPersistedKeys_CanDecryptStoredCredentials()
    {
        var keysDirectory = Path.Combine(Path.GetTempPath(), "smartpark-dp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(keysDirectory);

        try
        {
            using var db = CreateInMemoryDbContext();

            var firstProvider = BuildDataProtection(keysDirectory);
            var first = CreateStore(db, firstProvider, new PayOsOptions { Mode = PaymentIntegrationSettingsStore.SandboxMode });
            first.Save(PaymentIntegrationSettingsStore.LiveMode, LiveBaseUrl, "persist-client", "persist-api", "persist-key");

            // A brand-new provider (simulating container recreation) reads the same key ring.
            var secondProvider = BuildDataProtection(keysDirectory);
            var restarted = CreateStore(db, secondProvider, new PayOsOptions());

            var live = restarted.ForMode(PaymentIntegrationSettingsStore.LiveMode);
            Assert.Equal("persist-client", live.ClientId);
            Assert.Equal("persist-api", live.ApiKey);
            Assert.Equal("persist-key", live.ChecksumKey);
        }
        finally
        {
            Directory.Delete(keysDirectory, recursive: true);
        }
    }

    private sealed class NullTestLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
