using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace SmartParking.PaymentService.API.Authentication;

public sealed class PaymentJwksConfigurationManager(
    HttpClient client,
    Uri endpoint,
    string issuer,
    TimeProvider clock) : IConfigurationManager<OpenIdConnectConfiguration>
{
    private readonly object _sync = new();
    private OpenIdConnectConfiguration? _snapshot;
    private DateTimeOffset _freshUntil;
    private DateTimeOffset _retryAfter;
    private Task<OpenIdConnectConfiguration>? _refresh;
    private bool _refreshRequested;

    public void RequestRefresh()
    {
        lock (_sync)
        {
            _refreshRequested = true;
        }
    }

    public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel)
    {
        Task<OpenIdConnectConfiguration> pending;
        lock (_sync)
        {
            var now = clock.GetUtcNow();
            if (_snapshot is not null && now < _freshUntil && (!_refreshRequested || now < _retryAfter))
            {
                return _snapshot;
            }

            if (_refresh is { IsCompleted: false })
            {
                pending = _refresh;
            }
            else
            {
                if (now < _retryAfter)
                {
                    throw new PaymentJwksUnavailableException();
                }

                _retryAfter = now.AddSeconds(30);
                _refreshRequested = false;
                pending = _refresh = DownloadAsync();
            }
        }

        return await pending.WaitAsync(cancel);
    }

    private async Task<OpenIdConnectConfiguration> DownloadAsync()
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            using var response = await client.GetAsync(endpoint, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is > 65536)
            {
                throw new PaymentJwksUnavailableException();
            }

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var bytes = new MemoryStream();
            var buffer = new byte[4096];
            int read;
            while ((read = await stream.ReadAsync(buffer, timeout.Token)) != 0)
            {
                if (bytes.Length + read > 65536)
                {
                    throw new PaymentJwksUnavailableException();
                }

                bytes.Write(buffer, 0, read);
            }

            var set = new JsonWebKeySet(Encoding.UTF8.GetString(bytes.ToArray()));
            if (set.Keys.Count == 0 ||
                set.Keys.Select(k => k.Kid).Distinct(StringComparer.Ordinal).Count() != set.Keys.Count)
            {
                throw new PaymentJwksUnavailableException();
            }

            var next = new OpenIdConnectConfiguration { Issuer = issuer };
            foreach (var key in set.Keys)
            {
                if (key.Kty != "RSA" ||
                    key.Use != "sig" ||
                    key.Alg != "RS256" ||
                    string.IsNullOrWhiteSpace(key.Kid) ||
                    !string.IsNullOrEmpty(key.D) ||
                    string.IsNullOrEmpty(key.N) ||
                    string.IsNullOrEmpty(key.E))
                {
                    throw new PaymentJwksUnavailableException();
                }

                var modulus = Base64UrlEncoder.DecodeBytes(key.N);
                var exponent = Base64UrlEncoder.DecodeBytes(key.E);
                if (modulus.Length < 256 || exponent.Length == 0)
                {
                    throw new PaymentJwksUnavailableException();
                }

                using var rsa = RSA.Create();
                rsa.ImportParameters(new RSAParameters { Modulus = modulus, Exponent = exponent });
                next.SigningKeys.Add(key);
            }

            lock (_sync)
            {
                _snapshot = next;
                _freshUntil = clock.GetUtcNow().AddSeconds(300);
            }

            return next;
        }
        catch (Exception error) when (
            error is HttpRequestException or IOException or OperationCanceledException or
            ArgumentException or FormatException or JsonException or CryptographicException or
            PaymentJwksUnavailableException)
        {
            lock (_sync)
            {
                if (_snapshot is not null && clock.GetUtcNow() < _freshUntil)
                {
                    return _snapshot;
                }
            }

            throw new PaymentJwksUnavailableException();
        }
    }
}
