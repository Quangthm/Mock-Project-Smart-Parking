using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace SmartParking.ConsumerAuthentication;

public sealed class JwksUnavailableException : Exception
{
    public JwksUnavailableException() : base("Trusted authentication keys are unavailable.") { }
}

/// <summary>JWKS-only asynchronous discovery; never treats the endpoint as OIDC metadata.</summary>
public sealed class JwksConfigurationManager(HttpClient client, Uri endpoint, string issuer, TimeProvider clock)
    : IConfigurationManager<OpenIdConnectConfiguration>
{
    private readonly object sync = new();
    private OpenIdConnectConfiguration? snapshot;
    private DateTimeOffset freshUntil;
    private DateTimeOffset retryAfter;
    private Task<OpenIdConnectConfiguration>? refresh;
    private bool refreshRequested;

    public void RequestRefresh()
    {
        lock (sync) refreshRequested = true;
    }

    public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel)
    {
        Task<OpenIdConnectConfiguration> pending;
        lock (sync)
        {
            var now = clock.GetUtcNow();
            if (snapshot is not null && now < freshUntil && (!refreshRequested || now < retryAfter))
                return snapshot;
            if (refresh is { IsCompleted: false })
                pending = refresh;
            else
            {
                if (now < retryAfter) throw new JwksUnavailableException();
                // This throttle applies to successful unknown-kid refreshes as well as failures.
                retryAfter = now.AddSeconds(30);
                refreshRequested = false;
                pending = refresh = DownloadAsync();
            }
        }
        // Caller abort never cancels a refresh another request also needs.
        return await pending.WaitAsync(cancel);
    }

    private async Task<OpenIdConnectConfiguration> DownloadAsync()
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            using var response = await client.GetAsync(endpoint, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is > 65536) throw new JwksUnavailableException();
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var bytes = new MemoryStream();
            var buffer = new byte[4096];
            int read;
            while ((read = await stream.ReadAsync(buffer, timeout.Token)) != 0)
            {
                if (bytes.Length + read > 65536) throw new JwksUnavailableException();
                bytes.Write(buffer, 0, read);
            }
            var set = new JsonWebKeySet(System.Text.Encoding.UTF8.GetString(bytes.ToArray()));
            if (set.Keys.Count == 0 || set.Keys.Select(k => k.Kid).Distinct(StringComparer.Ordinal).Count() != set.Keys.Count)
                throw new JwksUnavailableException();
            var next = new OpenIdConnectConfiguration { Issuer = issuer };
            foreach (var key in set.Keys)
            {
                if (key.Kty != "RSA" || key.Use != "sig" || key.Alg != "RS256" || string.IsNullOrWhiteSpace(key.Kid) ||
                    !string.IsNullOrEmpty(key.D) || string.IsNullOrEmpty(key.N) || string.IsNullOrEmpty(key.E))
                    throw new JwksUnavailableException();
                var modulus = Base64UrlEncoder.DecodeBytes(key.N);
                var exponent = Base64UrlEncoder.DecodeBytes(key.E);
                if (modulus.Length < 256 || exponent.Length == 0) throw new JwksUnavailableException();
                using var rsa = System.Security.Cryptography.RSA.Create();
                rsa.ImportParameters(new() { Modulus = modulus, Exponent = exponent });
                next.SigningKeys.Add(key);
            }
            lock (sync)
            {
                snapshot = next;
                freshUntil = clock.GetUtcNow().AddSeconds(300);
            }
            return next;
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException or
            ArgumentException or FormatException or System.Text.Json.JsonException or System.Security.Cryptography.CryptographicException or JwksUnavailableException)
        {
            lock (sync)
            {
                // Failed refresh never replaces trusted keys. Only still-fresh keys may be used.
                if (snapshot is not null && clock.GetUtcNow() < freshUntil) return snapshot;
            }
            throw new JwksUnavailableException();
        }
    }
}
