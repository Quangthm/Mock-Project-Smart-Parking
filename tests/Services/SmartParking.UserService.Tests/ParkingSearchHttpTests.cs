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
using Microsoft.Extensions.Hosting;
using SmartParking.OperationService.API;
using SmartParking.OperationService.Application.Discovery;
using SmartParking.OperationService.Domain;

namespace SmartParking.UserService.Tests;

public sealed class ParkingSearchHttpTests
{
    [Fact]
    public async Task DisabledDiscoveryResolvesProductionDependenciesWithoutFixtureDataProtection()
    {
        await using var f = await SearchTestHost.Start(enabled: false, ephemeralProtection: false, validateServices: true);
        await f.Error(Path, 503);
    }
    [Theory]
    [InlineData("known", 1)]
    [InlineData("full", 0)]
    [InlineData("empty", 0)]
    [InlineData("partial", -1)]
    [InlineData("stale", -1)]
    [InlineData("coverage", -1)]
    public async Task CapacityEvidenceFlowsThroughActualSearchDi(string mode, int count)
    {
        await using var f = await SearchTestHost.Start(capacityMode: mode);
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

    [Fact]
    public async Task MotorcycleReturnsCatalogueAndPricingWithUnknownPooledCapacity()
    {
        await using var host = await SearchTestHost.Start();
        var rows = (await host.Data(Path.Replace("vehicleType=CAR", "vehicleType=MOTORCYCLE"))).GetProperty("items");
        Assert.Equal(2, rows.GetArrayLength());
        Assert.Equal("tariff", rows[0].GetProperty("price").GetProperty("kind").GetString());
        Assert.Equal(JsonValueKind.Null, rows[1].GetProperty("availability").GetProperty("availableCount").ValueKind);
    }

    [Theory]
    [InlineData("scope")]
    [InlineData("duplicate")]
    [InlineData("tenant")]
    [InlineData("oversized")]
    [InlineData("contradiction")]
    [InlineData("outage")]
    public async Task InvalidOrUnavailableCapacityCannotBecomeEmpty(string mode)
    {
        await using var f = await SearchTestHost.Start(capacityMode: mode);
        await f.Error(Path + "&availableOnly=true", 503);
    }

    [Fact]
    public async Task IntervalCoverageAndCapacityTimeoutCancellationUseRealDi()
    {
        await using var f = await SearchTestHost.Start(capacityMode: "known");
        var interval = Path + "&availableOnly=true&startAt=2031-01-01T01:00:00Z&endAt=2031-01-01T02:00:00Z";
        var item = Assert.Single((await f.Data(interval)).GetProperty("items").EnumerateArray());
        Assert.Equal("interval", item.GetProperty("availability").GetProperty("kind").GetString());
        Assert.Equal(2, item.GetProperty("availability").GetProperty("availableCount").GetInt32());
        Assert.Empty((await f.Data(Path + "&availableOnly=true&startAt=2031-01-02T00:00:00Z&endAt=2031-01-02T01:00:00Z"))
            .GetProperty("items").EnumerateArray());
        await using var timeout = await SearchTestHost.Start(capacityMode: "stall");
        await timeout.Error(Path, 504);
        Assert.True(timeout.CapacitySource!.Cancelled);
        await using var cancel = await SearchTestHost.Start(capacityMode: "stall");
        using var cts = new CancellationTokenSource();
        var pending = cancel.Client.GetAsync(Path, cts.Token);
        await cancel.CapacitySource!.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.True(cancel.CapacitySource.Cancelled);
    }
    private const string Path = "/api/parking-search?lat=10.123456&lon=106&vehicleType=CAR";
    internal static readonly Guid Far = new("00000000-0000-0000-0000-000000000001");
    internal static readonly Guid Near = new("00000000-0000-0000-0000-000000000002");
    internal static readonly Guid FirstVersion = new("73333333-3333-4333-8333-333333333333");

    [Fact]
    public async Task OrdersAcrossFacilityPagesAndReturnsProtectedContinuation()
    {
        await using var f = await SearchTestHost.Start();
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
        Assert.DoesNotContain(f.Logs.Messages, s => s.Contains("10.123456") || s.Contains("SYNTHETIC-DRIVER") || s.Contains(SearchTestHost.Secret));
    }

    [Fact]
    public async Task UnknownPriceAndStaleObservationsDoNotSatisfyFilters()
    {
        await using var f = await SearchTestHost.Start();
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
        await using var f = await SearchTestHost.Start();
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
        await using var f = await SearchTestHost.Start();
        f.Owners.PaymentStatus = ownerStatus;
        await f.Error(Path, status);
    }

    [Fact]
    public async Task MalformedOwnerAndEmptyCatalogueAndDisabledAreDistinct()
    {
        await using var f = await SearchTestHost.Start();
        f.Owners.BadPayment = true;
        await f.Error(Path, 503);
        f.Owners.Empty = true;
        Assert.Empty((await f.Data(Path)).GetProperty("items").EnumerateArray());
        await using var disabled = await SearchTestHost.Start(false);
        await disabled.Error(Path, 503);
        Assert.Equal(0, disabled.Owners.Calls);
    }

    [Fact]
    public async Task TimeoutAndClientAbortPropagateToPayment()
    {
        await using var f = await SearchTestHost.Start();
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
        await using var f = await SearchTestHost.Start();
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
        await using var f = await SearchTestHost.Start();
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
        await using var f = await SearchTestHost.Start();
        f.Owners.Stall = true;
        using var cancel = new CancellationTokenSource();
        var active = Enumerable.Range(0, 16).Select(_ => f.Client.GetAsync(Path, cancel.Token)).ToArray();
        using var wait = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (Volatile.Read(ref f.Owners.Stalled) < 16) await Task.Delay(10, wait.Token);
        await f.Error(Path, 429);
        cancel.Cancel();
        foreach (var pending in active) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

}
