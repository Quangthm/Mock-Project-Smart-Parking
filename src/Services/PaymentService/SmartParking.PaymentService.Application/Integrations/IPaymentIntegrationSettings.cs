namespace SmartParking.PaymentService.Application.Integrations;

/// <summary>
/// Immutable projection of the payment integration configuration for a single mode.
/// Credential values are raw and must never be logged or returned to callers unmasked.
/// </summary>
public sealed record PaymentIntegrationConfiguration(
    string Mode,
    string BaseUrl,
    string? ClientId,
    string? ApiKey,
    string? ChecksumKey,
    string? PreviousChecksumKey,
    bool IsConfigured,
    string Status,
    DateTimeOffset? LastTestedAt,
    long ConfigVersion);

/// <summary>
/// Immutable per-request configuration snapshot. A single snapshot is captured for one
/// adapter operation and used consistently so a concurrent mode/credential change can
/// never combine (for example) LIVE credentials with a FIXTURE base URL mid-request.
/// Credential values are raw and must never be logged or returned to callers unmasked.
/// </summary>
public sealed record PaymentConfigurationSnapshot(
    int Version,
    string Mode,
    string BaseUrl,
    string? ClientId,
    string? ApiKey,
    string? ChecksumKey);

/// <summary>
/// Pure application port describing server-side payment integration settings storage.
/// Implementations live in the infrastructure layer.
/// </summary>
public interface IPaymentIntegrationSettings
{
    PaymentIntegrationConfiguration GetCurrent();

    /// <summary>
    /// Captures one immutable snapshot of the active configuration. Callers must take a
    /// single snapshot per outbound operation and read mode, base URL and credentials
    /// only from it.
    /// </summary>
    PaymentConfigurationSnapshot GetCurrentSnapshot();

    PaymentIntegrationConfiguration ForMode(string mode);

    PaymentIntegrationConfiguration Save(
        string mode,
        string baseUrl,
        string? clientId,
        string? apiKey,
        string? checksumKey);

    IReadOnlyList<string> AllowedBaseUrls(string mode);

    /// <summary>
    /// Returns the single checksum key trusted to verify a webhook for an order that was
    /// created at <paramref name="orderCreatedAt"/> (from the retained keyring), or the
    /// current key when the timestamp is unknown. Returns null/blank when no key is
    /// trusted; callers must then reject the callback without any fallback key.
    /// </summary>
    string? GetTrustedWebhookChecksumKey(DateTimeOffset? orderCreatedAt);

    /// <summary>
    /// All non-blank secret values currently known (client ids, api keys, checksum keys,
    /// including retained/rotated keys) used to redact provider-supplied text before it is
    /// logged or returned. The collection itself is never logged.
    /// </summary>
    IReadOnlyCollection<string> GetSensitiveValues();

    /// <summary>
    /// Records a probe outcome, but only when <paramref name="configVersion"/> is still the
    /// active configuration version. Stale results from a superseded configuration are ignored.
    /// </summary>
    void MarkProbeResult(int configVersion, bool success);

    long NextConfigVersion();
}
