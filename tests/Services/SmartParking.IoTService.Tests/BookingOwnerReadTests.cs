using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.IoTService.Application.Assistant.Authorization;
using SmartParking.IoTService.Application.Assistant.Chat;
using SmartParking.IoTService.Infrastructure.Assistant;

namespace SmartParking.IoTService.Tests;

public sealed class BookingOwnerReadTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2032-02-28T17:00:00Z");
    private sealed record Vehicle(string VehicleType, string? DisplayLabel);
    private sealed record Booking(Guid ReservationId, string State, Guid SiteId, DateTimeOffset Start,
        DateTimeOffset End, DateTimeOffset CreatedAt, Vehicle? Vehicle);
    private static Booking Row(int i) => new(Guid.Parse($"11111111-1111-4111-8111-{i:D12}"), "CONFIRMED",
        Guid.Parse("22222222-2222-4222-8222-222222222222"), Now, Now.AddHours(1), Now.AddDays(-30).AddMinutes(-i), new("CAR", "SYNTHETIC"));

    [Fact]
    public async Task CanonicalPaginationAndDetailUseObservationTimeAndDoNotSkipLookahead()
    {
        var rows = Enumerable.Range(1, 21).Select(Row).ToArray();
        var transport = new Transport(request => {
            if (request.RequestUri!.AbsolutePath.EndsWith(rows[0].ReservationId.ToString())) return rows[0];
            var query = QueryHelpers.ParseQuery(request.RequestUri.Query);
            Assert.Equal("20", query["pageSize"].ToString());
            Assert.False(query.ContainsKey("limit")); Assert.False(query.ContainsKey("cursor"));
            if (!query.ContainsKey("beforeId")) return rows.Take(20).ToArray();
            Assert.Equal(rows[18].ReservationId.ToString(), query["beforeId"].ToString());
            Assert.Equal(rows[18].CreatedAt, DateTimeOffset.Parse(query["before"].ToString()));
            return rows.Skip(19).ToArray();
        });
        await using var provider = Services(transport, new TestActor());
        var client = provider.GetRequiredService<IOwnerReadTools>();
        var first = await client.Reservations(Now, Now.AddDays(1), null, default);
        Assert.Equal(19, first.Items.Length); Assert.NotNull(first.NextCursor);
        Assert.Equal(Now, first.AsOf); Assert.All(first.Items, row => Assert.Equal(Now, row.AsOf));
        var second = await client.Reservations(Now, Now.AddDays(1), first.NextCursor, default);
        Assert.Equal(rows.Skip(19).Select(x => x.ReservationId), second.Items.Select(x => x.Id));
        Assert.Null(second.NextCursor);
        var detail = await client.Detail(rows[0].ReservationId, default);
        Assert.Equal(rows[0].ReservationId, detail.Reservation.Id); Assert.Equal(Now, detail.AsOf);
        Assert.Equal(3, transport.Calls);
    }

    [Fact]
    public async Task InvalidOrReboundCursorFailsBeforeOwnerIo()
    {
        var actor = new TestActor();
        var transport = new Transport(_ => Enumerable.Range(1, 20).Select(Row).ToArray());
        await using var provider = Services(transport, actor);
        var client = provider.GetRequiredService<IOwnerReadTools>();
        var first = await client.Reservations(Now, Now.AddDays(1), null, default);
        foreach (var cursor in new[] { "forged", first.NextCursor + "x", new string('x', 2049) })
            Assert.Equal("invalid", (await Assert.ThrowsAsync<OwnerReadFailure>(() => client.Reservations(Now, Now.AddDays(1), cursor, default))).Code);
        Assert.Equal("invalid", (await Assert.ThrowsAsync<OwnerReadFailure>(() => client.Reservations(null, null, first.NextCursor, default))).Code);
        actor.Value = actor.Value with { AccountId = Guid.NewGuid() };
        Assert.Equal("invalid", (await Assert.ThrowsAsync<OwnerReadFailure>(() => client.Reservations(Now, Now.AddDays(1), first.NextCursor, default))).Code);
        Assert.Equal(1, transport.Calls);
    }

    [Theory]
    [InlineData("oversize")]
    [InlineData("duplicate")]
    [InlineData("order")]
    [InlineData("interval")]
    [InlineData("legacy")]
    public async Task MalformedCanonicalPagesFailClosed(string mode)
    {
        var rows = new[] { Row(1), Row(2) };
        object data = mode switch {
            "oversize" => Enumerable.Range(1, 21).Select(Row).ToArray(),
            "duplicate" => new[] { rows[0], rows[0] },
            "order" => rows.Reverse().ToArray(),
            "interval" => new[] { rows[0] with { Start = Now.AddDays(2), End = Now.AddDays(3) } },
            _ => new { items = rows, asOf = Now }
        };
        await using var provider = Services(new Transport(_ => data), new TestActor());
        var failure = await Assert.ThrowsAsync<OwnerReadFailure>(() => provider.GetRequiredService<IOwnerReadTools>().Reservations(Now, Now.AddDays(1), null, default));
        Assert.Equal("dependency_unavailable", failure.Code);
    }

    [Fact]
    public async Task EmptyIsSuccessfulOnlyForArrayAndMissingVehicleIsNotInvented()
    {
        var transport = new Transport(_ => Array.Empty<Booking>());
        await using var provider = Services(transport, new TestActor());
        var client = provider.GetRequiredService<IOwnerReadTools>();
        var page = await client.Reservations(null, null, null, default);
        Assert.Empty(page.Items); Assert.Null(page.NextCursor); Assert.Equal(Now, page.AsOf);
        transport.Response = _ => Row(1) with { Vehicle = null };
        Assert.Null((await client.Detail(Row(1).ReservationId, default)).Reservation.Vehicle);
        Assert.Equal("dependency_unavailable", (await Assert.ThrowsAsync<OwnerReadFailure>(() => client.Detail(Row(2).ReservationId, default))).Code);
    }

    private static ServiceProvider Services(Transport transport, TestActor actor)
    {
        var services = new ServiceCollection();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddSingleton<TimeProvider>(new Clock());
        services.AddSingleton<ICurrentActor>(actor); services.AddSingleton(new ReadToolOptions(Reservations: true));
        var context = new DefaultHttpContext(); context.Request.Headers.Authorization = "Bearer synthetic-token";
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = context });
        services.AddHttpClient("assistant-operations-read", c => c.BaseAddress = new Uri("https://synthetic.invalid"))
            .ConfigurePrimaryHttpMessageHandler(() => transport);
        services.AddTransient<IOwnerReadTools, OwnerReadClient>();
        return services.BuildServiceProvider();
    }
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class TestActor : ICurrentActor
    {
        public CurrentActor Value = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "driver", Now.AddHours(1));
        public CurrentActor Actor => Value;
    }
    private sealed class Transport(Func<HttpRequestMessage, object> response) : HttpMessageHandler
    {
        public Func<HttpRequestMessage, object> Response = response; public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++; Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { success = true, data = Response(request) }) });
        }
    }
}
