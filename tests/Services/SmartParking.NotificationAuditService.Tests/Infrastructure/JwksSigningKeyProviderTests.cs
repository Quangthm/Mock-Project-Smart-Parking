using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartParking.NotificationAuditService.Infrastructure.Options;
using SmartParking.NotificationAuditService.Infrastructure.Security;

namespace SmartParking.NotificationAuditService.Tests.Infrastructure;

public class JwksSigningKeyProviderTests
{
    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpClientFactory(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(new StubMessageHandler(_handler), disposeHandler: true);
        }
    }

    private sealed class StubMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

    private sealed class StaticOptionsMonitor : IOptionsMonitor<JwtValidationOptions>
    {
        public StaticOptionsMonitor(JwtValidationOptions currentValue) => CurrentValue = currentValue;
        public JwtValidationOptions CurrentValue { get; }
        public JwtValidationOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<JwtValidationOptions, string?> listener) => null;
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        public DateTimeOffset UtcNowValue { get; set; } = new DateTimeOffset(2026, 11, 26, 8, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => UtcNowValue;
    }

    private static string BuildJwksJson(string kid, RSA rsa)
    {
        var p = rsa.ExportParameters(false);
        string B64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        return JsonSerializer.Serialize(new
        {
            keys = new[]
            {
                new
                {
                    kty = "RSA",
                    kid,
                    use = "sig",
                    alg = "RS256",
                    n = B64(p.Modulus!),
                    e = B64(p.Exponent!)
                }
            }
        });
    }

    [Fact]
    public void ResolveKeys_WhenEndpointFailsOnColdStart_ShouldFailClosedAndThrottleRetries()
    {
        var calls = 0;
        var factory = new StubHttpClientFactory(_ =>
        {
            calls++;
            throw new HttpRequestException("Connection refused");
        });
        var time = new MutableTimeProvider();
        var options = new StaticOptionsMonitor(new JwtValidationOptions
        {
            JwksUri = "http://user-service:8080/.well-known/jwks.json",
            MinRefreshIntervalSeconds = 10
        });

        using var provider = new JwksSigningKeyProvider(factory, options, NullLogger<JwksSigningKeyProvider>.Instance, time);

        Assert.Empty(provider.ResolveKeys("kid-1"));
        Assert.Empty(provider.ResolveKeys("kid-1"));
        Assert.Equal(1, calls);

        time.UtcNowValue = time.UtcNowValue.AddSeconds(11);
        Assert.Empty(provider.ResolveKeys("kid-1"));
        Assert.Equal(2, calls);
    }

    [Fact]
    public void ResolveKeys_ShouldFetchCacheAndRetainValidKeysOnSubsequentMalformedRefresh()
    {
        using var rsa = RSA.Create(2048);
        var validJson = BuildJwksJson("smartpark-local-01", rsa);
        var returnMalformed = false;
        var calls = 0;

        var factory = new StubHttpClientFactory(_ =>
        {
            calls++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(returnMalformed ? "{\"keys\":[]}" : validJson, Encoding.UTF8, "application/json")
            };
        });

        var time = new MutableTimeProvider();
        var options = new StaticOptionsMonitor(new JwtValidationOptions
        {
            JwksUri = "http://user-service:8080/.well-known/jwks.json",
            CacheDurationSeconds = 60,
            MinRefreshIntervalSeconds = 10
        });

        using var provider = new JwksSigningKeyProvider(factory, options, NullLogger<JwksSigningKeyProvider>.Instance, time);

        // Initial fetch caches key
        var keys1 = provider.ResolveKeys("smartpark-local-01");
        Assert.Single(keys1);
        Assert.Equal("smartpark-local-01", keys1[0].Kid);

        // Within cache TTL -> served from cache without HTTP call
        var keys2 = provider.ResolveKeys("smartpark-local-01");
        Assert.Single(keys2);
        Assert.Equal(1, calls);

        // After TTL expires, malformed response must NOT wipe valid cached snapshot
        returnMalformed = true;
        time.UtcNowValue = time.UtcNowValue.AddSeconds(61);
        var keys3 = provider.ResolveKeys("smartpark-local-01");
        Assert.Single(keys3);
        Assert.Equal(2, calls);
    }
}
