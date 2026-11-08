using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using SmartParking.OperationService.API;
using SmartParking.OperationService.Application.Discovery;
using SmartParking.OperationService.Domain;

namespace SmartParking.UserService.Tests;

public sealed class ParkingSearchHttpTests
{
    [Theory]
    [InlineData("known", 1)]
    [InlineData("full", 0)]
    [InlineData("partial", -1)]
    [InlineData("stale", -1)]
    [InlineData("coverage", -1)]
    public async Task CapacityEvidenceFlowsThroughActualSearchDi(string mode, int count)
    {
        await using var f = await Host.Start(capacityMode: mode);
        var item = (await f.Data(Path)).GetProperty("items")[1];
        Assert.Equal(Far, item.GetProperty("siteId").GetGuid());
        var availability = item.GetProperty("availability");
        if (count >= 0)
        {
            Assert.Equal(count, availability.GetProperty("availableCount").GetInt32());
            Assert.Equal(JsonValueKind.Null, availability.GetProperty("reason").ValueKind);
            Assert.Equal("fresh", item.GetProperty("fieldStatus").GetProperty("availability").GetProperty("freshness").GetString());
        }
        else Assert.Equal(JsonValueKind.Null, availability.GetProperty("availableCount").ValueKind);
        var filtered = (await f.Data(Path + "&availableOnly=true")).GetProperty("items");
        Assert.Equal(count > 0 ? 1 : 0, filtered.GetArrayLength());
    }

    [Theory]
    [InlineData("scope")]
    [InlineData("duplicate")]
    [InlineData("outage")]
    public async Task InvalidOrUnavailableCapacityCannotBecomeEmpty(string mode)
    {
        await using var f = await Host.Start(capacityMode: mode);
        await f.Error(Path + "&availableOnly=true", 503);
    }

    private sealed class Capacity(Clock clock, string mode) : IDiscoveryCapacitySource
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Cancelled;
        public async Task<CapacityEvidence?> Read(DiscoverySite site, SearchFilter filter, DateTimeOffset at, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (mode == "stall")
            {
                Entered.TrySetResult();
                try { await Task.Delay(Timeout.InfiniteTimeSpan, ct); }
                finally { Cancelled = ct.IsCancellationRequested; }
            }
            if (mode == "outage") throw DiscoveryFailure.Unavailable();
            var a = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var b = Guid.Parse("22222222-2222-2222-2222-222222222222");
            CapacityBlock[] blocks = [new(a, at.AddHours(-1), at.AddHours(1)), new(a, at.AddMinutes(-1), at.AddMinutes(5))];
            if (mode == "full")
                blocks = [.. blocks, new(b, at.AddMinutes(-1), at.AddHours(1))];
            return new(mode == "scope" ? Guid.NewGuid() : site.SiteId,
                filter.VehicleType, site.ConfigurationVersion, "synthetic-policy", mode == "stale" ? clock.Now.AddMinutes(-2) : clock.Now,
                at.AddHours(-1), mode == "coverage" ? at : at.AddDays(1), mode != "partial",
                [new(a), new(mode == "duplicate" ? a : b)],
                blocks);
        }
    }

    [Fact]
    public async Task IntervalCoverageAndCapacityTimeoutCancellationUseRealDi()
    {
        await using var f = await Host.Start(capacityMode: "known");
        var interval = Path + "&availableOnly=true&startAt=2031-01-01T01:00:00Z&endAt=2031-01-01T02:00:00Z";
        var item = Assert.Single((await f.Data(interval)).GetProperty("items").EnumerateArray());
        Assert.Equal("interval", item.GetProperty("availability").GetProperty("kind").GetString());
        Assert.Equal(2, item.GetProperty("availability").GetProperty("availableCount").GetInt32());
        Assert.Empty((await f.Data(Path + "&availableOnly=true&startAt=2031-01-02T00:00:00Z&endAt=2031-01-02T01:00:00Z"))
            .GetProperty("items").EnumerateArray());
        await using var timeout = await Host.Start(capacityMode: "stall");
        await timeout.Error(Path, 504);
        Assert.True(timeout.CapacitySource!.Cancelled);
        await using var cancel = await Host.Start(capacityMode: "stall");
        using var cts = new CancellationTokenSource();
        var pending = cancel.Client.GetAsync(Path, cts.Token);
        await cancel.CapacitySource!.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.True(cancel.CapacitySource.Cancelled);
    }
    private const string Path = "/api/parking-search?lat=10.123456&lon=106&vehicleType=CAR";
    private static readonly Guid Far = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Near = new("00000000-0000-0000-0000-000000000002");
    private static readonly Guid FirstVersion = new("73333333-3333-4333-8333-333333333333");

    [Fact]
    public async Task OrdersAcrossFacilityPagesAndReturnsProtectedContinuation()
    {
        await using var f = await Host.Start();
        var page = await f.Data(Path + "&limit=1");
        var first = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal(Near, first.GetProperty("siteId").GetGuid());
        Assert.Equal(0, first.GetProperty("availability").GetProperty("availableCount").GetInt32());
        Assert.Equal(10000, first.GetProperty("price").GetProperty("amountVnd").GetInt64());
        var cursor = Uri.EscapeDataString(page.GetProperty("nextCursor").GetString()!);
        var next = Path + "&limit=1&cursor=" + cursor;
        var last = await f.Data(next);
        var far = Assert.Single(last.GetProperty("items").EnumerateArray());
        Assert.Equal(Far, far.GetProperty("siteId").GetGuid());
        Assert.Equal(JsonValueKind.Null, far.GetProperty("availability").GetProperty("availableCount").ValueKind);
        Assert.Equal(JsonValueKind.Null, last.GetProperty("nextCursor").ValueKind);
        Assert.Empty((await f.Data(Path + "&availableOnly=true")).GetProperty("items").EnumerateArray());
        await f.Error(next + "&radiusKm=1", 400);
        f.Owners.Version = Guid.NewGuid();
        await f.Error(next, 409);
        f.Owners.Version = FirstVersion;
        f.Clock.Now = f.Clock.Now.AddMinutes(5);
        await f.Error(next, 409);
        Assert.DoesNotContain(f.Logs.Messages, s => s.Contains("10.123456") || s.Contains("SYNTHETIC-DRIVER") || s.Contains(Host.Secret));
    }

    [Fact]
    public async Task UnknownPriceAndStaleObservationsDoNotSatisfyFilters()
    {
        await using var f = await Host.Start();
        var interval = Path + "&startAt=2031-01-01T01:00:00Z&endAt=2031-01-01T02:00:00Z";
        var items = (await f.Data(interval + "&minTotalVnd=0&maxTotalVnd=0")).GetProperty("items");
        var only = Assert.Single(items.EnumerateArray());
        Assert.Equal("estimate", only.GetProperty("price").GetProperty("kind").GetString());
        Assert.Equal("interval", only.GetProperty("availability").GetProperty("kind").GetString());
        f.Owners.StalePrice = true;
        var stale = (await f.Data(interval)).GetProperty("items")[0];
        Assert.Equal("stale", stale.GetProperty("fieldStatus").GetProperty("price").GetProperty("freshness").GetString());
        Assert.Equal(JsonValueKind.Null, stale.GetProperty("price").ValueKind);
        Assert.Empty((await f.Data(interval + "&minTotalVnd=0")).GetProperty("items").EnumerateArray());
        f.Owners.StaleFacility = true;
        await f.Error(Path, 503);
    }

    [Theory]
    [InlineData("&radiusKm=51")]
    [InlineData("&lotType=garage")]
    [InlineData("&lat=0")]
    [InlineData("&limit=51")]
    [InlineData("&startAt=2031-01-01T01:00:00Z")]
    [InlineData("&minTotalVnd=0")]
    [InlineData("&startAt=2031-01-01T00:00:00Z&endAt=2031-01-02T00:00:00.0000001Z")]
    [InlineData("&startAt=2031-01-08T00:00:00.0000001Z&endAt=2031-01-08T01:00:00Z")]
    [InlineData("&cursor=garbage")]
    public async Task InvalidQueriesNeverReachAnOwner(string suffix)
    {
        await using var f = await Host.Start();
        await f.Error(Path + suffix, 400);
        Assert.Equal(0, f.Owners.Calls);
    }

    [Theory]
    [InlineData(400, 503)]
    [InlineData(401, 503)]
    [InlineData(403, 503)]
    [InlineData(404, 409)]
    [InlineData(409, 409)]
    [InlineData(429, 429)]
    [InlineData(500, 503)]
    [InlineData(503, 503)]
    [InlineData(504, 504)]
    [InlineData(302, 503)]
    public async Task PaymentFailuresAreNeverEmptyOrGuestAuthentication(int ownerStatus, int status)
    {
        await using var f = await Host.Start();
        f.Owners.PaymentStatus = ownerStatus;
        await f.Error(Path, status);
    }

    [Fact]
    public async Task MalformedOwnerAndEmptyCatalogueAndDisabledAreDistinct()
    {
        await using var f = await Host.Start();
        f.Owners.BadPayment = true;
        await f.Error(Path, 503);
        f.Owners.Empty = true;
        Assert.Empty((await f.Data(Path)).GetProperty("items").EnumerateArray());
        await using var disabled = await Host.Start(false);
        await disabled.Error(Path, 503);
        Assert.Equal(0, disabled.Owners.Calls);
    }

    [Fact]
    public async Task TimeoutAndClientAbortPropagateToPayment()
    {
        await using var f = await Host.Start();
        f.Owners.Stall = true;
        await f.Error(Path, 504);
        Assert.True(f.Owners.SawCancellation);
        f.Owners.Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancel = new CancellationTokenSource();
        var pending = f.Client.GetAsync(Path, cancel.Token);
        await f.Owners.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    [Fact]
    public async Task TimeBoundsUnknownQueryAndAntimeridianAreHandled()
    {
        await using var f = await Host.Start();
        var end = "&startAt=2031-01-08T00:00:00Z&endAt=2031-01-09T00:00:00Z";
        Assert.Equal(2, (await f.Data(Path + end + "&unknownFilter=ignored")).GetProperty("items").GetArrayLength());
        f.Owners.Wrap = true;
        var wrapped = await f.Data("/api/parking-search?lat=0&lon=179.999&vehicleType=CAR");
        var item = Assert.Single(wrapped.GetProperty("items").EnumerateArray());
        Assert.InRange(item.GetProperty("distanceMeters").GetDouble(), 200, 250);
        using var post = await f.Client.PostAsJsonAsync("/api/parking-search", new { });
        Assert.Equal(HttpStatusCode.MethodNotAllowed, post.StatusCode);
        Assert.Equal("no-store", post.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task CandidateLoopRevisionDriftAndFutureObservationFailClosed()
    {
        await using var f = await Host.Start();
        f.Owners.Loop = true;
        await f.Error(Path, 503);
        f.Owners.Loop = false;
        f.Owners.Drift = true;
        await f.Error(Path, 409);
        f.Owners.Drift = false;
        f.Owners.Future = true;
        await f.Error(Path, 503);
    }

    [Fact]
    public async Task ConcurrencyLimitReturnsSafeNoStore429()
    {
        await using var f = await Host.Start();
        f.Owners.Stall = true;
        using var cancel = new CancellationTokenSource();
        var active = Enumerable.Range(0, 16).Select(_ => f.Client.GetAsync(Path, cancel.Token)).ToArray();
        using var wait = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (Volatile.Read(ref f.Owners.Calls) < 48) await Task.Delay(10, wait.Token);
        await f.Error(Path, 429);
        cancel.Cancel();
        foreach (var pending in active) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.Parse("2031-01-01T00:00:00Z");
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Owners(Clock clock) : HttpMessageHandler
    {
        public Guid Version = FirstVersion;
        public bool Empty, StalePrice, StaleFacility, BadPayment, Stall, SawCancellation, Wrap, Loop, Drift, Future;
        public int Calls, PaymentStatus;
        public TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Interlocked.Increment(ref Calls);
            Assert.Null(request.Headers.Authorization);
            Assert.False(request.Headers.Contains("X-Account-Id"));
            object data;
            if (request.RequestUri!.Host == "facility.invalid")
            {
                Assert.Equal("/api/public/parking-sites", request.RequestUri.AbsolutePath);
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.False(request.Headers.Contains("X-Discovery-Service-Key"));
                var first = !request.RequestUri.Query.Contains("cursor=");
                var stamp = StaleFacility ? clock.Now.AddHours(-1) : Future ? clock.Now.AddMinutes(1) : clock.Now;
                object row = new { siteId = first ? Far : Near, name = "Example", address = "Synthetic", latitude = first ? 10.13m : 10.123456m,
                    longitude = 106m, lotType = "outdoor", supportedVehicleTypes = new[] { "CAR" }, configurationVersion = FirstVersion, asOf = stamp,
                    serviceWindow = first ? null : new { state = "closed", from = clock.Now.AddDays(-1), to = clock.Now.AddDays(1), asOf = stamp } };
                if (Wrap) row = new { siteId = Near, name = "Wrap", address = "Synthetic", latitude = 0,
                    longitude = -179.999, lotType = "outdoor", supportedVehicleTypes = new[] { "CAR" },
                    configurationVersion = FirstVersion, asOf = stamp };
                data = new { items = Empty ? Array.Empty<object>() : new[] { row }, nextCursor = !Empty && !Wrap && (first || Loop) ? "next" : null,
                    catalogueVersion = !first && Drift ? Guid.NewGuid() : Version, asOf = stamp };
            }
            else
            {
                Assert.Equal("payment.invalid", request.RequestUri.Host);
                Assert.Equal("/internal/discovery/prices:read", request.RequestUri.AbsolutePath);
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal(Host.Secret, Assert.Single(request.Headers.GetValues("X-Discovery-Service-Key")));
                var body = await request.Content!.ReadFromJsonAsync<JsonElement>(ct);
                Assert.False(body.TryGetProperty("lat", out _));
                Assert.False(body.TryGetProperty("tenantId", out _));
                Assert.False(body.TryGetProperty("accountId", out _));
                if (PaymentStatus != 0) return new((HttpStatusCode)PaymentStatus) { Content = new StringContent("PRIVATE-UPSTREAM-BODY") };
                if (Stall)
                {
                    Started.TrySetResult();
                    try { await Task.Delay(Timeout.InfiniteTimeSpan, ct); }
                    finally { SawCancellation = ct.IsCancellationRequested; }
                }
                var interval = body.GetProperty("startAt").ValueKind != JsonValueKind.Null;
                var stamp = StalePrice ? clock.Now.AddMinutes(-10) : clock.Now;
                var rows = body.GetProperty("siteIds").EnumerateArray().Select(id => new { siteId = id.GetGuid(),
                    price = id.GetGuid() == Near ? new { kind = interval ? "estimate" : "tariff", billingUnit = interval ? "total" : "hour",
                        amountVnd = interval ? 0 : 10000, policyVersion = 1, asOf = stamp } : null,
                    reason = id.GetGuid() == Near ? null : "not_configured" }).ToArray();
                data = new { items = BadPayment ? rows.Take(1).ToArray() : rows, asOf = stamp };
            }
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { success = true, data }) };
        }
    }
    private sealed class Logs : ILoggerProvider, ILogger
    {
        public readonly System.Collections.Concurrent.ConcurrentBag<string> Messages = [];
        public ILogger CreateLogger(string categoryName) => this;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> format) =>
            Messages.Add(format(state, error));
        public void Dispose() { }
    }
    private sealed class Host : IAsyncDisposable
    {
        public const string Secret = "SYNTHETIC-PAYMENT-KEY-TEST-ONLY-0123456";
        public Clock Clock = new();
        public Owners Owners = null!;
        public Capacity? CapacitySource;
        public Logs Logs = new();
        public HttpClient Client = null!;
        private WebApplication app = null!;
        public static async Task<Host> Start(bool enabled = true, string? capacityMode = null)
        {
            var f = new Host();
            f.Owners = new(f.Clock);
            f.app = OperationHost.Build([], b =>
            {
                b.Configuration.Sources.Clear();
                b.Configuration.AddInMemoryCollection(new Dictionary<string,string?> {
                    ["Services:Key"] = new('k', 32), ["ConnectionStrings:Reservation"] = "Host=synthetic.invalid;Database=synthetic",
                    ["Discovery:Enabled"] = enabled.ToString(), ["Discovery:FacilityOrigin"] = "https://facility.invalid",
                    ["Discovery:PaymentOrigin"] = "https://payment.invalid", ["Discovery:PaymentServiceKey"] = Secret,
                    ["Discovery:MaxAgeSeconds"] = "60", ["Discovery:ClockSkewSeconds"] = "1"
                });
                b.WebHost.UseTestServer();
                b.Services.AddSingleton<TimeProvider>(f.Clock);
                if (capacityMode is not null)
                {
                    f.CapacitySource = new Capacity(f.Clock, capacityMode);
                    b.Services.AddSingleton<IDiscoveryCapacitySource>(f.CapacitySource);
                }
                b.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
                b.Logging.AddProvider(f.Logs);
                foreach (var name in new[] { "discovery-facility", "discovery-payment" })
                    b.Services.PostConfigure<HttpClientFactoryOptions>(name, o => o.HttpMessageHandlerBuilderActions.Add(x => x.PrimaryHandler = f.Owners));
            });
            await f.app.StartAsync();
            f.Client = f.app.GetTestClient();
            f.Client.DefaultRequestHeaders.Authorization = new("Bearer", "SYNTHETIC-DRIVER");
            f.Client.DefaultRequestHeaders.Add("X-Account-Id", Guid.NewGuid().ToString());
            return f;
        }
        public async Task<JsonElement> Data(string path)
        {
            using var res = await Client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Equal("no-store", res.Headers.CacheControl?.ToString());
            return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").Clone();
        }
        public async Task Error(string path, int status)
        {
            using var res = await Client.GetAsync(path);
            Assert.Equal(status, (int)res.StatusCode);
            Assert.Equal("no-store", res.Headers.CacheControl?.ToString());
            var root = await res.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(root.GetProperty("success").GetBoolean());
            Assert.False(root.TryGetProperty("data", out _));
            Assert.DoesNotContain("PRIVATE-UPSTREAM-BODY", root.ToString());
        }
        public async ValueTask DisposeAsync() { Client.Dispose(); await app.DisposeAsync(); }
    }
}
