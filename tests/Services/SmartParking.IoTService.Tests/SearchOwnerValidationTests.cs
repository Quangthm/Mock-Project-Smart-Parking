using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.IoTService.Application.Assistant.Authorization;
using SmartParking.IoTService.Application.Assistant.Chat;
using SmartParking.IoTService.Infrastructure.Assistant;

namespace SmartParking.IoTService.Tests;

public sealed class SearchOwnerValidationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-06T03:00:00Z");
    private static readonly Guid First = Guid.Parse("11111111-1111-4111-8111-111111111111");

    [Theory]
    [InlineData("old-page")] [InlineData("future-page")] [InlineData("old-site")]
    [InlineData("inconsistent-price-time")] [InlineData("inconsistent-capacity-time")]
    [InlineData("reverse-order")] [InlineData("wrong-filter")] [InlineData("unknown-price-filter")]
    public async Task MalformedPublicOwnerEvidenceFailsWithoutPrivateCredential(string scenario)
    {
        var row = Row();
        var page = new ParkingSearchPage([row], null, Now);
        var query = new ParkingSearch(10, 105, "CAR", 5, false, null, null, null, null, null);
        switch (scenario)
        {
            case "old-page": page = page with { AsOf = Now.AddSeconds(-61) }; break;
            case "future-page": page = page with { AsOf = Now.AddSeconds(3) }; break;
            case "old-site": page = page with { Items = [row with { SourceAsOf = row.SourceAsOf with { Facility = Now.AddSeconds(-61) } }] }; break;
            case "inconsistent-price-time":
                page = page with { Items = [row with { Price = new("tariff", 10000, "hour", 1, Now),
                    FieldStatus = row.FieldStatus with { Price = new("fresh", null) }, SourceAsOf = row.SourceAsOf with { Payment = Now.AddSeconds(-1) } }] };
                break;
            case "inconsistent-capacity-time":
                page = page with { Items = [row with { Availability = new("current", 0, Now, null),
                    FieldStatus = row.FieldStatus with { Availability = new("fresh", null) }, SourceAsOf = row.SourceAsOf with { Operations = Now.AddSeconds(-1) } }] };
                break;
            case "reverse-order": page = page with { Items = [row with { SiteId = Guid.Parse("22222222-2222-4222-8222-222222222222"), DistanceMeters = 11 }, row] }; break;
            case "wrong-filter": query = query with { LotType = "basement" }; break;
            case "unknown-price-filter": query = query with { MinTotalVnd = 0, StartAt = Now, EndAt = Now.AddHours(1) };
                page = page with { Items = [row with { Availability = row.Availability with { Kind = "interval" } }] }; break;
        }
        var transport = new ResponseHandler(page);
        await using var provider = Services(transport);
        var client = provider.GetRequiredService<IOwnerReadTools>();
        var failure = await Assert.ThrowsAsync<OwnerReadFailure>(() => client.Search(query, null, CancellationToken.None));
        Assert.Equal("dependency_unavailable", failure.Code);
        Assert.Equal(1, transport.Calls);
    }

    [Fact]
    public async Task ExactAgeAndFutureSkewBoundsAreAccepted()
    {
        var row = Row() with { SourceAsOf = Row().SourceAsOf with { Facility = Now.AddSeconds(-60) } };
        var transport = new ResponseHandler(new([row], null, Now.AddSeconds(2)));
        await using var provider = Services(transport);
        var page = await provider.GetRequiredService<IOwnerReadTools>()
            .Search(new(10, 105, "CAR", 5, false, null, null, null, null, null), null, CancellationToken.None);
        Assert.Single(page.Items);
    }

    private static ParkingSummary Row() => new(First, "Synthetic", "Synthetic", 10, 105, 10, "outdoor", ["CAR"],
        new("current", null, null, "unknown"), null, new(Now, null, Now),
        new(new("fresh", null), new("unknown", "unknown"), new("unknown", "not_configured")));

    private static ServiceProvider Services(ResponseHandler transport)
    {
        var services = new ServiceCollection();
        services.AddDataProtection();
        services.AddSingleton<TimeProvider>(new Clock());
        services.AddSingleton<ICurrentActor>(new TestActor());
        services.AddSingleton(new ReadToolOptions(false, false, true, 60, 2));
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = new DefaultHttpContext() });
        services.AddHttpClient("assistant-parking-search", c => c.BaseAddress = new Uri("https://synthetic.invalid"))
            .RemoveAllLoggers().ConfigurePrimaryHttpMessageHandler(() => transport);
        services.AddTransient<IOwnerReadTools, OwnerReadClient>();
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext!.Request.Headers.Authorization = "Bearer PRIVATE-CREDENTIAL";
        return provider;
    }
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class TestActor : ICurrentActor
    {
        public CurrentActor Actor { get; } = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "driver", Now.AddHours(1));
    }
    private sealed class ResponseHandler(ParkingSearchPage page) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Assert.Null(request.Headers.Authorization);
            Assert.Equal("/api/parking-search", request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { success = true, data = page }) });
        }
    }
}
