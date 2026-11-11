using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using SmartParking.OperationService.Infrastructure.Authentication;

namespace SmartParking.FacilityService.Tests;

public sealed class ConsumerJwksCacheTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 11, 9, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Transport : HttpMessageHandler
    {
        public int Calls;
        public Func<Task<HttpResponseMessage>> Reply = () => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Interlocked.Increment(ref Calls);
            Assert.Equal("https://issuer.example/keys", request.RequestUri!.AbsoluteUri);
            return Reply();
        }
    }
    private static HttpResponseMessage Keys(string kid)
    {
        using var rsa = RSA.Create(2048);
        var key = rsa.ExportParameters(false);
        return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new
        {
            keys = new[] { new { kid, kty = "RSA", use = "sig", alg = "RS256",
                n = Base64UrlEncoder.Encode(key.Modulus!), e = Base64UrlEncoder.Encode(key.Exponent!) } }
        })) };
    }
    private static JwksConfigurationManager Manager(Transport transport, Clock clock) =>
        new(new HttpClient(transport), new Uri("https://issuer.example/keys"), "issuer", clock);

    [Fact]
    public async Task ColdOutageFailsClosedAndRecoversAfterBackoffWithoutRestart()
    {
        var transport = new Transport(); var clock = new Clock(); var manager = Manager(transport, clock);
        Assert.Equal(0, transport.Calls);
        await Assert.ThrowsAsync<JwksUnavailableException>(() => manager.GetConfigurationAsync(default));
        transport.Reply = () => Task.FromResult(Keys("first"));
        await Assert.ThrowsAsync<JwksUnavailableException>(() => manager.GetConfigurationAsync(default));
        Assert.Equal(1, transport.Calls);
        clock.Now = clock.Now.AddSeconds(31);
        Assert.Single((await manager.GetConfigurationAsync(default)).SigningKeys);
        Assert.Equal(2, transport.Calls);
    }

    [Fact]
    public async Task MalformedRefreshKeepsFreshKeysButNeverExtendsFreshness()
    {
        var transport = new Transport { Reply = () => Task.FromResult(Keys("first")) };
        var clock = new Clock(); var manager = Manager(transport, clock);
        var initial = await manager.GetConfigurationAsync(default);
        clock.Now = clock.Now.AddSeconds(31);
        transport.Reply = () => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"keys\":[]}") });
        manager.RequestRefresh();
        Assert.Same(initial, await manager.GetConfigurationAsync(default));
        clock.Now = clock.Now.AddSeconds(270);
        await Assert.ThrowsAsync<JwksUnavailableException>(() => manager.GetConfigurationAsync(default));
    }

    [Fact]
    public async Task ConcurrentRefreshIsSharedAndCallerCancellationDoesNotPoisonIt()
    {
        var completion = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = new Transport { Reply = () => completion.Task };
        var manager = Manager(transport, new Clock());
        using var cancellation = new CancellationTokenSource();
        var cancelled = manager.GetConfigurationAsync(cancellation.Token);
        var others = Enumerable.Range(0, 12).Select(_ => manager.GetConfigurationAsync(default)).ToArray();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        completion.SetResult(Keys("first"));
        var results = await Task.WhenAll(others);
        Assert.Equal(1, transport.Calls);
        Assert.All(results, result => Assert.Same(results[0], result));
    }
}
