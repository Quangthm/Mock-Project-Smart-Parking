using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartParking.PaymentService.Application.Common.Interfaces;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.Integrations;
using SmartParking.PaymentService.Infrastructure.Options;

namespace SmartParking.PaymentService.Infrastructure.Services;

/// <summary>
/// Thread-safe settings store for PayOS integration. Live and Sandbox credentials are kept
/// in separate slots and are never copied across modes. An immutable per-request snapshot is
/// exposed for outbound operations, a retained keyring supports in-flight webhook verification
/// during key rotation, and accepted changes are persisted as redacted
/// <c>policy_revisions</c> rows for UC-POL-02.
/// </summary>
public sealed class PaymentIntegrationSettingsStore : IPaymentIntegrationSettings
{
    public const string LiveMode = "LIVE";
    public const string SandboxMode = "SANDBOX";
    public const string FixtureMode = "FIXTURE";

    public const string StatusHealthy = "HEALTHY";
    public const string StatusMisconfigured = "MISCONFIGURED";
    public const string StatusUntested = "UNTESTED";
    public const string StatusDegraded = "DEGRADED";

    private const string DefaultPayOsBaseUrl = "https://api-merchant.payos.vn";

    /// <summary>Deterministic policy id grouping all PAYMENT_INTEGRATION revisions.</summary>
    public static readonly Guid PaymentIntegrationPolicyId =
        Guid.Parse("7f9c3b2a-1d4e-4f6a-8b2c-0d1e2f3a4b5c");

    public const string PaymentIntegrationPolicyType = "PAYMENT_INTEGRATION";

    /// <summary>Minimum retention window for a rotated checksum key (SSOT §3.4, >= 24h).</summary>
    public static readonly TimeSpan KeyRetentionWindow = TimeSpan.FromHours(24);

    private const string CredentialProtectionPurpose = "SmartParking.PaymentIntegration.Credentials.v1";

    private static readonly string[] LiveAndSandboxBaseUrls = [DefaultPayOsBaseUrl];
    private static readonly string[] FixtureBaseUrls = ["http://localhost:5173", "https://fixture.internal"];

    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly JsonSerializerOptions PayloadReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly object _gate = new();
    private readonly Dictionary<string, string> _baseUrls;

    private string _mode;

    private string _liveClientId;
    private string _liveApiKey;
    private string _liveChecksumKey;
    private string _livePreviousChecksumKey;

    private string _sandboxClientId;
    private string _sandboxApiKey;
    private string _sandboxChecksumKey;
    private string _sandboxPreviousChecksumKey;

    private readonly Dictionary<string, List<RetainedChecksumKey>> _keyrings;

    private readonly Dictionary<string, bool?> _probeSuccess;
    private readonly Dictionary<string, DateTimeOffset?> _probeAt;


    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly IDataProtectionProvider? _dataProtectionProvider;
    private readonly ILogger<PaymentIntegrationSettingsStore>? _logger;
    private bool _hydrated;

    private long _configVersion = 1;

    public PaymentIntegrationSettingsStore(
        IOptions<PayOsOptions> options,
        IServiceScopeFactory? scopeFactory = null,
        IDataProtectionProvider? dataProtectionProvider = null,
        ILogger<PaymentIntegrationSettingsStore>? logger = null)
    {
        _logger = logger;
        var value = options.Value ?? new PayOsOptions();

        _scopeFactory = scopeFactory;
        _dataProtectionProvider = dataProtectionProvider;

        _mode = NormalizeMode(value.Mode);

        var baseUrl = string.IsNullOrWhiteSpace(value.BaseUrl) ? DefaultPayOsBaseUrl : value.BaseUrl;
        _baseUrls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [LiveMode] = baseUrl,
            [SandboxMode] = baseUrl,
            [FixtureMode] = baseUrl,
        };

        // Mode-isolated seed credentials. Sandbox falls back to the legacy
        // PayOs:ClientId/ApiKey/ChecksumKey settings when sandbox-specific
        // values are not supplied. Live credentials never fall back.
        _liveClientId = value.LiveClientId ?? string.Empty;
        _liveApiKey = value.LiveApiKey ?? string.Empty;
        _liveChecksumKey = value.LiveChecksumKey ?? string.Empty;
        _livePreviousChecksumKey = value.PreviousChecksumKey ?? string.Empty;

        _sandboxClientId = FirstNonEmpty(value.SandboxClientId, value.ClientId);
        _sandboxApiKey = FirstNonEmpty(value.SandboxApiKey, value.ApiKey);
        _sandboxChecksumKey = FirstNonEmpty(value.SandboxChecksumKey, value.ChecksumKey);
        _sandboxPreviousChecksumKey = value.PreviousChecksumKey ?? string.Empty;

        _keyrings = new Dictionary<string, List<RetainedChecksumKey>>(StringComparer.OrdinalIgnoreCase)
        {
            [LiveMode] = SeedKeyring(_liveChecksumKey, _livePreviousChecksumKey),
            [SandboxMode] = SeedKeyring(_sandboxChecksumKey, _sandboxPreviousChecksumKey),
            [FixtureMode] = new List<RetainedChecksumKey>(),
        };

        _probeSuccess = new Dictionary<string, bool?>(StringComparer.OrdinalIgnoreCase)
        {
            [LiveMode] = null,
            [SandboxMode] = null,
            [FixtureMode] = null,
        };
        _probeAt = new Dictionary<string, DateTimeOffset?>(StringComparer.OrdinalIgnoreCase)
        {
            [LiveMode] = null,
            [SandboxMode] = null,
            [FixtureMode] = null,
        };
    }

    public PaymentIntegrationConfiguration GetCurrent()
    {
        lock (_gate)
        {
            EnsureHydratedLocked();
            return BuildConfiguration(_mode);
        }
    }

    public PaymentConfigurationSnapshot GetCurrentSnapshot()
    {
        lock (_gate)
        {
            EnsureHydratedLocked();
            var configuration = BuildConfiguration(_mode);
            return new PaymentConfigurationSnapshot(
                Version: (int)configuration.ConfigVersion,
                Mode: configuration.Mode,
                BaseUrl: configuration.BaseUrl,
                ClientId: configuration.ClientId,
                ApiKey: configuration.ApiKey,
                ChecksumKey: configuration.ChecksumKey);
        }
    }

    public PaymentIntegrationConfiguration ForMode(string mode)
    {
        lock (_gate)
        {
            EnsureHydratedLocked();
            return BuildConfiguration(NormalizeMode(mode));
        }
    }

    public PaymentIntegrationConfiguration Save(
        string mode,
        string baseUrl,
        string? clientId,
        string? apiKey,
        string? checksumKey)
    {
        var normalized = NormalizeMode(mode);
        var now = DateTimeOffset.UtcNow;
        PaymentIntegrationConfiguration result;
        PersistedStoreState persistedState;
        StoreState rollback;

        lock (_gate)
        {
            EnsureHydratedLocked();

            rollback = CaptureState();

            _mode = normalized;
            _baseUrls[normalized] = baseUrl;

            // Only the slot for the saved mode is touched. Null means "keep the
            // stored value"; the handler rejects blank-but-provided values.
            switch (normalized)
            {
                case LiveMode:
                    if (clientId is not null) _liveClientId = clientId;
                    if (apiKey is not null) _liveApiKey = apiKey;
                    if (checksumKey is not null)
                    {
                        // Rotation: retain the superseded key for the webhook grace window.
                        if (!string.Equals(_liveChecksumKey, checksumKey, StringComparison.Ordinal))
                        {
                            _livePreviousChecksumKey = _liveChecksumKey;
                            RotateKeyring(LiveMode, checksumKey, now);
                        }
                        _liveChecksumKey = checksumKey;
                    }
                    break;
                case SandboxMode:
                    if (clientId is not null) _sandboxClientId = clientId;
                    if (apiKey is not null) _sandboxApiKey = apiKey;
                    if (checksumKey is not null)
                    {
                        // Rotation: retain the superseded key for the webhook grace window.
                        if (!string.Equals(_sandboxChecksumKey, checksumKey, StringComparison.Ordinal))
                        {
                            _sandboxPreviousChecksumKey = _sandboxChecksumKey;
                            RotateKeyring(SandboxMode, checksumKey, now);
                        }
                        _sandboxChecksumKey = checksumKey;
                    }
                    break;
                // FIXTURE stores no credentials.
            }

            // A fresh save discards any previous probe result for that mode.
            _probeSuccess[normalized] = null;
            _probeAt[normalized] = null;

            _configVersion++;

            result = BuildConfiguration(normalized);
            persistedState = BuildPersistedStateLocked(normalized);

            // Readers and competing saves must not observe this candidate until its
            // append commits. Keep rollback inside the same gate as publication.
            try
            {
                PersistAcceptedConfiguration(normalized, result, persistedState, now);
            }
            catch (Exception ex)
            {
                RestoreState(rollback);
                _logger?.LogError("Payment integration persistence failed ({FailureType}); configuration rolled back.", ex.GetType().Name);
                throw new PaymentIntegrationPersistenceException(
                    "The payment integration configuration could not be persisted. Retry the request.", ex);
            }
            return result;
        }
    }

    public IReadOnlyList<string> AllowedBaseUrls(string mode)
    {
        return NormalizeMode(mode) == FixtureMode ? FixtureBaseUrls : LiveAndSandboxBaseUrls;
    }

    public string? GetTrustedWebhookChecksumKey(DateTimeOffset? orderCreatedAt)
    {
        lock (_gate)
        {
            EnsureHydratedLocked();

            string? MatchIn(List<RetainedChecksumKey> keyring)
            {
                if (keyring.Count == 0) return null;
                if (orderCreatedAt is null)
                {
                    var current = keyring.LastOrDefault(k => k.RetiredAt is null) ?? keyring[^1];
                    return string.IsNullOrWhiteSpace(current.Key) ? null : current.Key;
                }

                var at = orderCreatedAt.Value;
                var match = keyring
                    .Where(k => k.RetainedFrom <= at && at <= k.RetainedUntil)
                    .OrderByDescending(k => k.RetiredAt is null)
                    .ThenByDescending(k => k.RetainedFrom)
                    .FirstOrDefault();

                return match is null || string.IsNullOrWhiteSpace(match.Key) ? null : match.Key;
            }

            // 1. Current mode's keyring
            if (_keyrings.TryGetValue(_mode, out var currentKeyring))
            {
                var key = MatchIn(currentKeyring);
                if (!string.IsNullOrWhiteSpace(key)) return key;
            }

            // 2. Retained keyrings of other modes (e.g. Order was created in SANDBOX, then Admin switched to LIVE)
            foreach (var kvp in _keyrings)
            {
                if (string.Equals(kvp.Key, _mode, StringComparison.OrdinalIgnoreCase)) continue;
                var key = MatchIn(kvp.Value);
                if (!string.IsNullOrWhiteSpace(key)) return key;
            }

            // 3. Fallback to active key of current mode
            var (_, _, activeKey) = GetCredentials(_mode);
            return string.IsNullOrWhiteSpace(activeKey) ? null : activeKey;
        }
    }

    public IReadOnlyCollection<string> GetSensitiveValues()
    {
        lock (_gate)
        {
            EnsureHydratedLocked();

            var values = new HashSet<string>(StringComparer.Ordinal);

            void Add(string? candidate)
            {
                if (!string.IsNullOrWhiteSpace(candidate)) values.Add(candidate);
            }

            Add(_liveClientId);
            Add(_liveApiKey);
            Add(_liveChecksumKey);
            Add(_livePreviousChecksumKey);
            Add(_sandboxClientId);
            Add(_sandboxApiKey);
            Add(_sandboxChecksumKey);
            Add(_sandboxPreviousChecksumKey);

            foreach (var keyring in _keyrings.Values)
            {
                foreach (var key in keyring) Add(key.Key);
            }

            return values.ToArray();
        }
    }

    public void MarkProbeResult(int configVersion, bool success)
    {
        lock (_gate)
        {
            // A probe that started under a superseded configuration must not overwrite the
            // health of the configuration that replaced it.
            if (_configVersion != configVersion) return;

            _probeSuccess[_mode] = success;
            _probeAt[_mode] = DateTimeOffset.UtcNow;
        }
    }

    public long NextConfigVersion()
    {
        lock (_gate)
        {
            return ++_configVersion;
        }
    }

    private PaymentIntegrationConfiguration BuildConfiguration(string mode)
    {
        var baseUrl = _baseUrls.TryGetValue(mode, out var storedBaseUrl) ? storedBaseUrl : DefaultPayOsBaseUrl;

        // FIXTURE is always healthy, needs no credentials, and makes zero outbound calls.
        if (mode == FixtureMode)
        {
            return new PaymentIntegrationConfiguration(
                Mode: mode,
                BaseUrl: baseUrl,
                ClientId: null,
                ApiKey: null,
                ChecksumKey: null,
                PreviousChecksumKey: null,
                IsConfigured: true,
                Status: StatusHealthy,
                LastTestedAt: null,
                ConfigVersion: _configVersion);
        }

        var (clientId, apiKey, checksumKey) = GetCredentials(mode);
        var previousChecksumKey = GetPreviousChecksumKey(mode);
        var isConfigured =
            !string.IsNullOrWhiteSpace(clientId) &&
            !string.IsNullOrWhiteSpace(apiKey) &&
            !string.IsNullOrWhiteSpace(checksumKey);

        string status;
        DateTimeOffset? lastTestedAt;
        if (!isConfigured)
        {
            status = StatusMisconfigured;
            lastTestedAt = null;
        }
        else
        {
            var probe = _probeSuccess.TryGetValue(mode, out var success) ? success : null;
            status = probe switch
            {
                true => StatusHealthy,
                false => StatusDegraded,
                null => StatusUntested,
            };
            lastTestedAt = probe is null
                ? null
                : (_probeAt.TryGetValue(mode, out var at) ? at : null);
        }

        return new PaymentIntegrationConfiguration(
            Mode: mode,
            BaseUrl: baseUrl,
            ClientId: clientId,
            ApiKey: apiKey,
            ChecksumKey: checksumKey,
            PreviousChecksumKey: previousChecksumKey,
            IsConfigured: isConfigured,
            Status: status,
            LastTestedAt: lastTestedAt,
            ConfigVersion: _configVersion);
    }

    private (string ClientId, string ApiKey, string ChecksumKey) GetCredentials(string mode) => mode switch
    {
        LiveMode => (_liveClientId, _liveApiKey, _liveChecksumKey),
        SandboxMode => (_sandboxClientId, _sandboxApiKey, _sandboxChecksumKey),
        _ => (string.Empty, string.Empty, string.Empty),
    };

    private string GetPreviousChecksumKey(string mode) => mode switch
    {
        LiveMode => _livePreviousChecksumKey,
        SandboxMode => _sandboxPreviousChecksumKey,
        _ => string.Empty,
    };

    private static List<RetainedChecksumKey> SeedKeyring(string checksumKey, string previousChecksumKey)
    {
        // The store cannot know when an option-configured key actually became active, so it
        // anchors the trust window to load time. An order created before the retention window
        // predates any checksum key this process could have issued, so it must not be trusted
        // (otherwise a legacy seeded key would validate arbitrarily old callbacks).
        var retainedFrom = DateTimeOffset.UtcNow - KeyRetentionWindow;
        var list = new List<RetainedChecksumKey>();
        if (!string.IsNullOrWhiteSpace(previousChecksumKey))
        {
            list.Add(new RetainedChecksumKey(
                previousChecksumKey,
                retainedFrom,
                DateTimeOffset.MinValue,
                DateTimeOffset.MaxValue));
        }
        if (!string.IsNullOrWhiteSpace(checksumKey))
        {
            list.Add(new RetainedChecksumKey(
                checksumKey,
                retainedFrom,
                null,
                DateTimeOffset.MaxValue));
        }
        return list;
    }

    private void RotateKeyring(string mode, string newKey, DateTimeOffset now)
    {
        if (!_keyrings.TryGetValue(mode, out var keyring))
        {
            keyring = new List<RetainedChecksumKey>();
            _keyrings[mode] = keyring;
        }

        // Retire the current key, retaining it for at least the SSOT §3.4 window.
        var current = keyring.LastOrDefault(k => k.RetiredAt is null);
        if (current is not null)
        {
            current.RetiredAt = now;
            current.RetainedUntil = now + KeyRetentionWindow;
        }

        keyring.Add(new RetainedChecksumKey(
            newKey,
            now,
            null,
            DateTimeOffset.MaxValue));
    }

    public Task HydrateAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _hydrated = false;
            EnsureHydratedLocked();
        }
        return Task.CompletedTask;
    }

    private void PersistAcceptedConfiguration(
        string mode,
        PaymentIntegrationConfiguration configuration,
        PersistedStoreState state,
        DateTimeOffset now)
    {
        if (_scopeFactory is null || _dataProtectionProvider is null) return;

        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetService<IPaymentIntegrationHistoryRepository>();
        if (repository is null) return;

        var actorId = scope.ServiceProvider.GetService<ICurrentActor>()?.UserId ?? Guid.Empty;
        var protector = _dataProtectionProvider.CreateProtector(CredentialProtectionPurpose);

        // Read the saved mode's credentials from the immutable snapshot taken under the lock,
        // so a concurrent save cannot mix values into this revision.
        var modeState = state.Modes.TryGetValue(mode, out var capturedMode) ? capturedMode : null;
        var credentials = new IntegrationCredentials(
            modeState?.ClientId,
            modeState?.ApiKey,
            modeState?.ChecksumKey,
            modeState?.PreviousChecksumKey);

        // The v2 state blob contains every mode's credentials and the full retained
        // keyrings (all historical keys), protected as one opaque value. The legacy
        // single-mode fields are still written so a rollback to older code keeps working.
        var stateJson = JsonSerializer.Serialize(state, PayloadJsonOptions);
        var payload = JsonSerializer.Serialize(
            new PersistedIntegrationPayload(
                Mode: mode,
                BaseUrl: configuration.BaseUrl,
                CredentialsProtected: protector.Protect(JsonSerializer.Serialize(credentials)),
                StateProtected: protector.Protect(stateJson),
                StateVersion: PersistedStoreState.CurrentVersion),
            PayloadJsonOptions);

        repository
            .AppendAsync(PaymentIntegrationPolicyId, payload, actorId, now, CancellationToken.None)
            .GetAwaiter().GetResult();
    }

    private void EnsureHydratedLocked()
    {
        if (_hydrated) return;

        if (_scopeFactory is null || _dataProtectionProvider is null)
        {
            _hydrated = true;
            return;
        }

        var previousState = CaptureState();
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetService<IPaymentIntegrationHistoryRepository>();
            if (repository is null)
            {
                throw new InvalidOperationException("Payment integration history storage is not registered.");
            }

            var latest = repository
                .GetLatestAsync(PaymentIntegrationPolicyId, CancellationToken.None)
                .GetAwaiter().GetResult();

            if (latest is not null && !string.IsNullOrWhiteSpace(latest.PolicyValuesJson))
            {
                ApplyPersistedPayloadLocked(latest.PolicyValuesJson);
            }

            // Only a successful read (and successful decrypt/apply) marks hydration complete.
            // A transient failure leaves the flag false so the next access retries.
            _hydrated = true;
        }
        catch (Exception ex)
        {
            RestoreState(previousState);
            _hydrated = false;
            _logger?.LogWarning("Payment integration hydration failed ({FailureType}); access requires a successful retry.", ex.GetType().Name);
            throw new PaymentIntegrationPersistenceException(
                "The saved payment integration configuration is temporarily unavailable. Retry the request.", ex);
        }
    }

    private void ApplyPersistedPayloadLocked(string policyValuesJson)
    {
        using var doc = JsonDocument.Parse(policyValuesJson);
        var root = doc.RootElement;

        string? GetStringProp(string camel, string pascal)
        {
            if (root.TryGetProperty(camel, out var cp) && cp.ValueKind == JsonValueKind.String)
                return cp.GetString();
            if (root.TryGetProperty(pascal, out var pp) && pp.ValueKind == JsonValueKind.String)
                return pp.GetString();
            return null;
        }

        var mode = NormalizeMode(GetStringProp("mode", "Mode"));
        var baseUrl = GetStringProp("baseUrl", "BaseUrl");
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            _baseUrls[mode] = baseUrl;
        }

        var protector = _dataProtectionProvider!.CreateProtector(CredentialProtectionPurpose);

        // Preferred: v2 full-state blob restores ALL modes' credentials and keyrings.
        var protectedState = GetStringProp("stateProtected", "StateProtected");
        if (!string.IsNullOrWhiteSpace(protectedState))
        {
            var stateJson = protector.Unprotect(protectedState);
            var state = JsonSerializer.Deserialize<PersistedStoreState>(stateJson, PayloadReadOptions);
            ApplyPersistedStateLocked(state);
            return;
        }

        // Backward compatibility: historical single-mode revision.
        _mode = mode;
        var protectedCreds = GetStringProp("credentialsProtected", "CredentialsProtected");
        if (!string.IsNullOrWhiteSpace(protectedCreds))
        {
            var credentialsJson = protector.Unprotect(protectedCreds);
            var credentials = JsonSerializer.Deserialize<IntegrationCredentials>(
                credentialsJson,
                PayloadReadOptions);
            ApplyCredentials(mode, credentials);
        }
    }

    private void ApplyPersistedStateLocked(PersistedStoreState? state)
    {
        if (state?.Modes is null) return;

        foreach (var (modeKey, modeState) in state.Modes)
        {
            var mode = NormalizeMode(modeKey);
            if (modeState is null) continue;

            if (!string.IsNullOrWhiteSpace(modeState.BaseUrl))
            {
                _baseUrls[mode] = modeState.BaseUrl;
            }

            if (mode == FixtureMode) continue;

            var clientId = modeState.ClientId ?? string.Empty;
            var apiKey = modeState.ApiKey ?? string.Empty;
            var checksumKey = modeState.ChecksumKey ?? string.Empty;
            var previousChecksumKey = modeState.PreviousChecksumKey ?? string.Empty;

            if (mode == LiveMode)
            {
                _liveClientId = clientId;
                _liveApiKey = apiKey;
                _liveChecksumKey = checksumKey;
                _livePreviousChecksumKey = previousChecksumKey;
            }
            else
            {
                _sandboxClientId = clientId;
                _sandboxApiKey = apiKey;
                _sandboxChecksumKey = checksumKey;
                _sandboxPreviousChecksumKey = previousChecksumKey;
            }

            _keyrings[mode] = RestoreKeyring(modeState, checksumKey, previousChecksumKey);
        }

        _mode = NormalizeMode(state.Mode);
        if (state.ConfigVersion > 0)
        {
            _configVersion = state.ConfigVersion;
        }
    }

    private static List<RetainedChecksumKey> RestoreKeyring(
        PersistedModeState modeState,
        string checksumKey,
        string previousChecksumKey)
    {
        if (modeState.Keyring is { Count: > 0 })
        {
            var restored = modeState.Keyring
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Key))
                .Select(entry => new RetainedChecksumKey(
                    entry.Key,
                    entry.RetainedFrom,
                    entry.RetiredAt,
                    entry.RetainedUntil))
                .ToList();

            if (restored.Count > 0) return restored;
        }

        return SeedKeyring(checksumKey, previousChecksumKey);
    }

    private void ApplyCredentials(string mode, IntegrationCredentials? credentials)
    {
        if (credentials is null) return;

        switch (mode)
        {
            case LiveMode:
                _liveClientId = credentials.ClientId ?? string.Empty;
                _liveApiKey = credentials.ApiKey ?? string.Empty;
                _liveChecksumKey = credentials.ChecksumKey ?? string.Empty;
                _livePreviousChecksumKey = credentials.PreviousChecksumKey ?? string.Empty;
                _keyrings[LiveMode] = SeedKeyring(_liveChecksumKey, _livePreviousChecksumKey);
                break;
            case SandboxMode:
                _sandboxClientId = credentials.ClientId ?? string.Empty;
                _sandboxApiKey = credentials.ApiKey ?? string.Empty;
                _sandboxChecksumKey = credentials.ChecksumKey ?? string.Empty;
                _sandboxPreviousChecksumKey = credentials.PreviousChecksumKey ?? string.Empty;
                _keyrings[SandboxMode] = SeedKeyring(_sandboxChecksumKey, _sandboxPreviousChecksumKey);
                break;
        }
    }

    private PersistedStoreState BuildPersistedStateLocked(string mode) => new(
        Version: PersistedStoreState.CurrentVersion,
        Mode: mode,
        Modes: new Dictionary<string, PersistedModeState>(StringComparer.OrdinalIgnoreCase)
        {
            [LiveMode] = new PersistedModeState(
                BaseUrl: _baseUrls[LiveMode],
                ClientId: _liveClientId,
                ApiKey: _liveApiKey,
                ChecksumKey: _liveChecksumKey,
                PreviousChecksumKey: _livePreviousChecksumKey,
                Keyring: SnapshotKeyring(LiveMode)),
            [SandboxMode] = new PersistedModeState(
                BaseUrl: _baseUrls[SandboxMode],
                ClientId: _sandboxClientId,
                ApiKey: _sandboxApiKey,
                ChecksumKey: _sandboxChecksumKey,
                PreviousChecksumKey: _sandboxPreviousChecksumKey,
                Keyring: SnapshotKeyring(SandboxMode)),
            [FixtureMode] = new PersistedModeState(
                BaseUrl: _baseUrls[FixtureMode],
                ClientId: null,
                ApiKey: null,
                ChecksumKey: null,
                PreviousChecksumKey: null,
                Keyring: SnapshotKeyring(FixtureMode)),
        },
        ConfigVersion: _configVersion);

    private List<PersistedKeyringEntry> SnapshotKeyring(string mode) =>
        _keyrings.TryGetValue(mode, out var keyring)
            ? keyring.Select(key => new PersistedKeyringEntry(
                key.Key,
                key.RetainedFrom,
                key.RetiredAt,
                key.RetainedUntil)).ToList()
            : new List<PersistedKeyringEntry>();

    private StoreState CaptureState() => new(
        Mode: _mode,
        BaseUrls: new Dictionary<string, string>(_baseUrls, StringComparer.OrdinalIgnoreCase),
        LiveClientId: _liveClientId,
        LiveApiKey: _liveApiKey,
        LiveChecksumKey: _liveChecksumKey,
        LivePreviousChecksumKey: _livePreviousChecksumKey,
        SandboxClientId: _sandboxClientId,
        SandboxApiKey: _sandboxApiKey,
        SandboxChecksumKey: _sandboxChecksumKey,
        SandboxPreviousChecksumKey: _sandboxPreviousChecksumKey,
        Keyrings: _keyrings.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Select(key => new RetainedChecksumKey(
                key.Key,
                key.RetainedFrom,
                key.RetiredAt,
                key.RetainedUntil)).ToList(),
            StringComparer.OrdinalIgnoreCase),
        ProbeSuccess: new Dictionary<string, bool?>(_probeSuccess, StringComparer.OrdinalIgnoreCase),
        ProbeAt: new Dictionary<string, DateTimeOffset?>(_probeAt, StringComparer.OrdinalIgnoreCase),
        ConfigVersion: _configVersion);

    private void RestoreState(StoreState state)
    {
        _mode = state.Mode;

        _baseUrls.Clear();
        foreach (var pair in state.BaseUrls) _baseUrls[pair.Key] = pair.Value;

        _liveClientId = state.LiveClientId;
        _liveApiKey = state.LiveApiKey;
        _liveChecksumKey = state.LiveChecksumKey;
        _livePreviousChecksumKey = state.LivePreviousChecksumKey;

        _sandboxClientId = state.SandboxClientId;
        _sandboxApiKey = state.SandboxApiKey;
        _sandboxChecksumKey = state.SandboxChecksumKey;
        _sandboxPreviousChecksumKey = state.SandboxPreviousChecksumKey;

        _keyrings.Clear();
        foreach (var pair in state.Keyrings)
        {
            _keyrings[pair.Key] = pair.Value;
        }

        _probeSuccess.Clear();
        foreach (var pair in state.ProbeSuccess) _probeSuccess[pair.Key] = pair.Value;

        _probeAt.Clear();
        foreach (var pair in state.ProbeAt) _probeAt[pair.Key] = pair.Value;

        _configVersion = state.ConfigVersion;
    }

    private static string NormalizeMode(string? mode)
    {
        var normalized = mode?.Trim().ToUpperInvariant();
        return normalized switch
        {
            LiveMode => LiveMode,
            SandboxMode => SandboxMode,
            FixtureMode => FixtureMode,
            _ => SandboxMode,
        };
    }

    private static string FirstNonEmpty(string? preferred, string? fallback) =>
        !string.IsNullOrWhiteSpace(preferred) ? preferred : fallback ?? string.Empty;

    private sealed class RetainedChecksumKey
    {
        public RetainedChecksumKey(
            string key,
            DateTimeOffset retainedFrom,
            DateTimeOffset? retiredAt,
            DateTimeOffset retainedUntil)
        {
            Key = key;
            RetainedFrom = retainedFrom;
            RetiredAt = retiredAt;
            RetainedUntil = retainedUntil;
        }

        public string Key { get; }
        public DateTimeOffset RetainedFrom { get; }
        public DateTimeOffset? RetiredAt { get; set; }
        public DateTimeOffset RetainedUntil { get; set; }
    }

    private sealed record IntegrationCredentials(
        string? ClientId,
        string? ApiKey,
        string? ChecksumKey,
        string? PreviousChecksumKey);

    private sealed record PersistedIntegrationPayload(
        string Mode,
        string BaseUrl,
        string CredentialsProtected,
        string? StateProtected,
        int? StateVersion);

    private sealed record PersistedStoreState(
        int Version,
        string Mode,
        Dictionary<string, PersistedModeState> Modes,
        long ConfigVersion)
    {
        public const int CurrentVersion = 2;
    }

    private sealed record PersistedModeState(
        string BaseUrl,
        string? ClientId,
        string? ApiKey,
        string? ChecksumKey,
        string? PreviousChecksumKey,
        List<PersistedKeyringEntry> Keyring);

    private sealed record PersistedKeyringEntry(
        string Key,
        DateTimeOffset RetainedFrom,
        DateTimeOffset? RetiredAt,
        DateTimeOffset RetainedUntil);

    private sealed record StoreState(
        string Mode,
        Dictionary<string, string> BaseUrls,
        string LiveClientId,
        string LiveApiKey,
        string LiveChecksumKey,
        string LivePreviousChecksumKey,
        string SandboxClientId,
        string SandboxApiKey,
        string SandboxChecksumKey,
        string SandboxPreviousChecksumKey,
        Dictionary<string, List<RetainedChecksumKey>> Keyrings,
        Dictionary<string, bool?> ProbeSuccess,
        Dictionary<string, DateTimeOffset?> ProbeAt,
        long ConfigVersion);
}
