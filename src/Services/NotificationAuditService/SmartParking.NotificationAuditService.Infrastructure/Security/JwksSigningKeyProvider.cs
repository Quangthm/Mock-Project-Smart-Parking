using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartParking.NotificationAuditService.Infrastructure.Options;

namespace SmartParking.NotificationAuditService.Infrastructure.Security;

public sealed class JwksSigningKeyProvider : IJwksSigningKeyProvider, IDisposable
{
    public const string HttpClientName = "JwksClient";

    private sealed record CachedKeySnapshot(IReadOnlyList<JwksRsaKey> Keys, DateTimeOffset FetchedAt);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<JwtValidationOptions> _optionsMonitor;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<JwksSigningKeyProvider> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private CachedKeySnapshot? _snapshot;
    private DateTimeOffset _lastRefreshAttempt = DateTimeOffset.MinValue;

    public JwksSigningKeyProvider(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<JwtValidationOptions> optionsMonitor,
        ILogger<JwksSigningKeyProvider> logger,
        TimeProvider? timeProvider = null)
    {
        _httpClientFactory = httpClientFactory;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_optionsMonitor.CurrentValue.JwksUri);

    public IReadOnlyList<JwksRsaKey> ResolveKeys(string? kid)
    {
        var options = _optionsMonitor.CurrentValue;
        if (string.IsNullOrWhiteSpace(options.JwksUri))
        {
            return Array.Empty<JwksRsaKey>();
        }

        var now = _timeProvider.GetUtcNow();
        var current = Volatile.Read(ref _snapshot);
        var cacheTtl = TimeSpan.FromSeconds(Math.Max(1, options.CacheDurationSeconds));
        var isExpired = current is null || (now - current.FetchedAt) >= cacheTtl;
        var hasMatchingKid = !string.IsNullOrWhiteSpace(kid) &&
                             current?.Keys.Any(k => string.Equals(k.Kid, kid, StringComparison.Ordinal)) == true;

        var needsRefresh = isExpired || (!string.IsNullOrWhiteSpace(kid) && !hasMatchingKid);
        if (needsRefresh)
        {
            var minInterval = TimeSpan.FromSeconds(Math.Max(1, options.MinRefreshIntervalSeconds));
            if (_lastRefreshAttempt == DateTimeOffset.MinValue || (now - _lastRefreshAttempt) >= minInterval)
            {
                RefreshAsync(options, kid, CancellationToken.None).GetAwaiter().GetResult();
                current = Volatile.Read(ref _snapshot);
            }
        }

        if (current is null)
        {
            return Array.Empty<JwksRsaKey>();
        }

        if (!string.IsNullOrWhiteSpace(kid))
        {
            var matched = current.Keys
                .Where(k => string.Equals(k.Kid, kid, StringComparison.Ordinal))
                .ToList();
            return matched;
        }

        return current.Keys;
    }

    private async Task RefreshAsync(JwtValidationOptions options, string? requestedKid, CancellationToken cancellationToken)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            var now = _timeProvider.GetUtcNow();
            var current = Volatile.Read(ref _snapshot);
            var cacheTtl = TimeSpan.FromSeconds(Math.Max(1, options.CacheDurationSeconds));
            var isExpired = current is null || (now - current.FetchedAt) >= cacheTtl;
            var hasMatchingKid = !string.IsNullOrWhiteSpace(requestedKid) &&
                                 current?.Keys.Any(k => string.Equals(k.Kid, requestedKid, StringComparison.Ordinal)) == true;

            if (!isExpired && (string.IsNullOrWhiteSpace(requestedKid) || hasMatchingKid))
            {
                return;
            }

            var minInterval = TimeSpan.FromSeconds(Math.Max(1, options.MinRefreshIntervalSeconds));
            if (_lastRefreshAttempt != DateTimeOffset.MinValue && (now - _lastRefreshAttempt) < minInterval)
            {
                return;
            }

            _lastRefreshAttempt = now;

            if (!Uri.TryCreate(options.JwksUri, UriKind.Absolute, out var jwksUri))
            {
                _logger.LogWarning("Configured JwtValidation:JwksUri is not a valid absolute URI.");
                return;
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, options.RequestTimeoutSeconds)));

            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync(jwksUri, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("JWKS endpoint {JwksUri} returned HTTP {StatusCode}.", jwksUri, (int)response.StatusCode);
                return;
            }

            var json = await response.Content.ReadAsStringAsync(timeoutCts.Token);
            var parsedKeys = ParseJwks(json);
            if (parsedKeys.Count == 0)
            {
                _logger.LogWarning("JWKS endpoint {JwksUri} returned zero valid RS256 signing keys. Retaining previous snapshot if any.", jwksUri);
                return;
            }

            Volatile.Write(ref _snapshot, new CachedKeySnapshot(parsedKeys, _timeProvider.GetUtcNow()));
            _logger.LogInformation("Refreshed {KeyCount} RS256 public key(s) from JWKS endpoint {JwksUri}.", parsedKeys.Count, jwksUri);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or FormatException or CryptographicException)
        {
            _logger.LogWarning(ex, "Failed to refresh JWKS signing keys from {JwksUri}. Failing closed / retaining valid cached keys.", options.JwksUri);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private static IReadOnlyList<JwksRsaKey> ParseJwks(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("keys", out var keysElement) || keysElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<JwksRsaKey>();
        }

        var keys = new List<JwksRsaKey>();
        foreach (var item in keysElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var kty = item.TryGetProperty("kty", out var ktyEl) ? ktyEl.GetString() : null;
            if (!string.Equals(kty, "RSA", StringComparison.Ordinal))
            {
                continue;
            }

            if (item.TryGetProperty("use", out var useEl))
            {
                var use = useEl.GetString();
                if (!string.IsNullOrWhiteSpace(use) && !string.Equals(use, "sig", StringComparison.Ordinal))
                {
                    continue;
                }
            }

            if (item.TryGetProperty("alg", out var algEl))
            {
                var alg = algEl.GetString();
                if (!string.IsNullOrWhiteSpace(alg) && !string.Equals(alg, "RS256", StringComparison.Ordinal))
                {
                    continue;
                }
            }

            var kid = item.TryGetProperty("kid", out var kidEl) ? kidEl.GetString() : null;
            var nStr = item.TryGetProperty("n", out var nEl) ? nEl.GetString() : null;
            var eStr = item.TryGetProperty("e", out var eEl) ? eEl.GetString() : null;

            if (string.IsNullOrWhiteSpace(kid) || string.IsNullOrWhiteSpace(nStr) || string.IsNullOrWhiteSpace(eStr))
            {
                continue;
            }

            var modulus = DecodeBase64Url(nStr);
            var exponent = DecodeBase64Url(eStr);
            if (modulus.Length == 0 || exponent.Length == 0)
            {
                continue;
            }

            keys.Add(new JwksRsaKey(kid, new RSAParameters
            {
                Modulus = modulus,
                Exponent = exponent
            }));
        }

        return keys;
    }

    private static byte[] DecodeBase64Url(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2:
                padded += "==";
                break;
            case 3:
                padded += "=";
                break;
        }

        return Convert.FromBase64String(padded);
    }

    public void Dispose() => _refreshLock.Dispose();
}
