using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.PaymentService.API;
using SmartParking.PaymentService.API.Controllers;
using SmartParking.PaymentService.API.Middleware;
using SmartParking.PaymentService.Application;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Persistence;
using SmartParking.PaymentService.Persistence.Adapters;

namespace SmartParking.PaymentService.Tests.Integration;

public sealed class DiscoveryPriceHttpTests
{
    private static readonly Guid Site = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid NoTariff = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private const string Good = "synthetic-current-discovery-only-key";
    private const string Old = "synthetic-rotated-discovery-only-key";
    private const string Instant = "2026-11-06T11:00:00Z"; // 18:00 locally
    private const string Policy = """
        {"timeZoneId":"Asia/Ho_Chi_Minh","roundingMode":"HALF_UP_THOUSAND",
        "discoveryEstimate":{"graceMinutes":0,"minimumVnd":0},"timeBlocks":[
        {"blockCode":"day","startTime":"06:00","endTime":"18:00","ratePerHourVnd":10000},
        {"blockCode":"night","startTime":"18:00","endTime":"06:00","ratePerHourVnd":20000}]}
        """;

    [Fact]
    public async Task CurrentAndIntervalChooseTheEffectiveRevisionAndPreserveUnknown()
    {
        await using var app = await Host(db =>
        {
            Add(db, Policy, 1, "2026-11-01T00:00:00Z", "2026-11-10T00:00:00Z");
            Add(db, Policy.Replace("20000", "0"), 2, "2026-11-10T00:00:00Z", null);
        });
        using var client = app.GetTestClient();
        var current = await Success(client, new { siteIds = new[] { Site, NoTariff }, vehicleType = "CAR", at = Instant });
        Assert.Equal(20000, current[0].GetProperty("price").GetProperty("amountVnd").GetInt64());
        Assert.Equal("hour", current[0].GetProperty("price").GetProperty("billingUnit").GetString());
        Assert.Equal("not_configured", current[1].GetProperty("reason").GetString());
        Assert.Equal(JsonValueKind.Null, current[1].GetProperty("price").ValueKind);
        var next = await Success(client, new { siteIds = new[] { Site }, vehicleType = "CAR", at = "2026-11-10T00:00:00Z" });
        Assert.Equal(2, next[0].GetProperty("price").GetProperty("policyVersion").GetInt32());
        Assert.Equal(10000, next[0].GetProperty("price").GetProperty("amountVnd").GetInt64());
        var zero = await Success(client, new { siteIds = new[] { Site }, vehicleType = "CAR", at = "2026-11-10T11:00:00Z" });
        Assert.Equal(0, zero[0].GetProperty("price").GetProperty("amountVnd").GetInt64());
        var interval = await Success(client, Window("2026-11-06T10:30:00Z", "2026-11-06T11:30:00Z"));
        Assert.Equal(15000, interval[0].GetProperty("price").GetProperty("amountVnd").GetInt64());
        Assert.Equal("estimate", interval[0].GetProperty("price").GetProperty("kind").GetString());
        var crossing = await Success(client, Window("2026-11-09T10:00:00Z", "2026-11-10T10:00:00Z"));
        Assert.Equal("not_supported", crossing[0].GetProperty("reason").GetString());
    }

    [Theory]
    [InlineData("rate", 503)] [InlineData("gap", 503)] [InlineData("overlap", 503)]
    [InlineData("foreignTenant", 503)] [InlineData("zone", 200)] [InlineData("noDiscoveryTerms", 200)]
    public async Task MalformedPolicyAndUnknownHaveDifferentOutcomes(string scenario, int status)
    {
        await using var app = await Host(db =>
        {
            var value = scenario switch
            {
                "rate" => Policy.Replace("\"ratePerHourVnd\":10000", "\"extra\":7"),
                "gap" => Policy.Replace("\"startTime\":\"18:00\"", "\"startTime\":\"19:00\""),
                "noDiscoveryTerms" => Policy.Replace("discoveryEstimate", "other"),
                _ => Policy
            };
            Add(db, value, 1, "2026-01-01T00:00:00Z", null, tenant: scenario == "foreignTenant" ? Guid.NewGuid() : Tenant);
            if (scenario is "zone" or "overlap")
                Add(db, value, 2, "2026-02-01T00:00:00Z", null, scenario == "zone" ? "ZONE" : "SITE");
        });
        using var client = app.GetTestClient();
        using var response = await Send(client, Window("2026-11-06T10:30:00Z", "2026-11-06T11:30:00Z"));
        Assert.Equal(status, (int)response.StatusCode);
        if (status == 200)
        {
            var item = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("items")[0];
            Assert.Equal(JsonValueKind.Null, item.GetProperty("price").ValueKind);
            Assert.Equal("not_supported", item.GetProperty("reason").GetString());
        }
        else Assert.DoesNotContain(Tenant.ToString(), await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TrustTransportValidationAndScopeAreSeparate()
    {
        await using var app = await Host(_ => { });
        using var client = app.GetTestClient();
        object valid = new { siteIds = new[] { Site }, vehicleType = "CAR", at = Instant };
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(client, valid, key: null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(client, valid, key: Old)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(client, valid, bearer: Good)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(client, valid, https: false)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, new { siteIds = new[] { Guid.NewGuid() }, vehicleType = "CAR", at = Instant })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, new { siteIds = new[] { Site, Site }, vehicleType = "CAR", at = Instant })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, new { siteIds = new[] { Site }, vehicleType = "CAR", at = "2026-11-06T12:00:00" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, new { siteIds = new[] { Site }, vehicleType = "CAR", at = Instant, startAt = Instant })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, Window("2026-11-06T00:00:00Z", "2026-11-07T00:00:00.0000001Z"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, Window("2026-11-13T00:00:00.0000001Z", "2026-11-13T01:00:00Z"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, Window("2026-11-13T00:00:00Z", "2026-11-14T00:00:00Z"))).StatusCode);
        foreach (var route in new[] { "/api/v1/pricing/calculate", "/api/v1/payments/orders" })
        {
            using var protectedRequest = new HttpRequestMessage(HttpMethod.Post, "https://synthetic.local" + route) { Content = JsonContent.Create(new { }) };
            protectedRequest.Headers.Add(DiscoveryPriceHosting.Header, Good);
            protectedRequest.Headers.Authorization = new("Bearer", "synthetic-operator");
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(protectedRequest)).StatusCode);
        }
        app.Configuration["DiscoveryPricing:Operations:Previous:ExpiresAt"] = "2090-01-01T00:00:00Z";
        Assert.Equal(HttpStatusCode.OK, (await Send(client, valid, key: Old)).StatusCode);
    }

    private static object Window(string startAt, string endAt) => new { siteIds = new[] { Site }, vehicleType = "CAR", at = Instant, startAt, endAt };

    private static Task<HttpResponseMessage> Send(HttpClient client, object body, string? key = Good, string? bearer = null, bool https = true)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, (https ? "https" : "http") + "://synthetic.local" + DiscoveryPriceHosting.Route)
        { Content = JsonContent.Create(body) };
        if (key != null) request.Headers.Add(DiscoveryPriceHosting.Header, key);
        if (bearer != null) request.Headers.Authorization = new("Bearer", bearer);
        return client.SendAsync(request);
    }

    private static async Task<JsonElement> Success(HttpClient client, object body)
    {
        using var response = await Send(client, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("items");
    }

    private static async Task<WebApplication> Host(Action<PaymentDbContext> seed)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DiscoveryPricing:Operations:Current:Key"] = Good, ["DiscoveryPricing:Operations:Current:ExpiresAt"] = "2090-01-01T00:00:00Z",
            ["DiscoveryPricing:Operations:Previous:Key"] = Old, ["DiscoveryPricing:Operations:Previous:ExpiresAt"] = "2000-01-01T00:00:00Z",
            [$"DiscoveryPricing:SiteTenants:{Site}"] = Tenant.ToString(), [$"DiscoveryPricing:SiteTenants:{NoTariff}"] = Tenant.ToString()
        });
        var name = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<PaymentDbContext>(o => o.UseInMemoryDatabase(name));
        builder.Services.AddApplicationServices();
        builder.Services.AddScoped<IDiscoveryPriceSource, DiscoveryPriceSource>();
        builder.Services.AddDiscoveryPriceReads();
        builder.Services.AddSingleton<TimeProvider>(new FixedClock());
        builder.Services.AddAuthentication(DevAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, DevAuthenticationHandler>(DevAuthenticationHandler.SchemeName, null);
        builder.Services.AddAuthorization();
        builder.Services.AddControllers().AddApplicationPart(typeof(PricingController).Assembly);
        var host = builder.Build();
        host.UseDiscoveryPriceReads();
        host.UseAuthentication();
        host.UseAuthorization();
        host.MapControllers();
        host.MapDiscoveryPriceReads();
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        seed(db);
        await db.SaveChangesAsync();
        await host.StartAsync();
        return host;
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.Parse("2026-11-06T00:00:00Z");
    }

    private static void Add(PaymentDbContext db, string json, int version, string from, string? to, string scope = "SITE", Guid? tenant = null)
    {
        var tariff = new Tariff { Id = Guid.NewGuid(), TenantId = tenant ?? Tenant, SiteId = Site, Name = "Synthetic", VehicleType = "CAR" };
        db.Tariffs.Add(tariff);
        db.PolicyRevisions.Add(new PolicyRevision
        {
            Id = Guid.NewGuid(), PolicyId = tariff.Id, EffectiveFrom = DateTimeOffset.Parse(from),
            EffectiveTo = to is null ? null : DateTimeOffset.Parse(to), RevisionNumber = version,
            ScopeType = scope, ScopeId = scope == "SITE" ? Site : Guid.NewGuid(), PolicyValuesJson = json
        });
    }
}
