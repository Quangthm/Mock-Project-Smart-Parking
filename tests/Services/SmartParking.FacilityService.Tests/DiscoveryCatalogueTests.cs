using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartParking.FacilityService.API;
using SmartParking.FacilityService.Application;
using SmartParking.FacilityService.Domain;
using SmartParking.FacilityService.Infrastructure;

namespace SmartParking.FacilityService.Tests;

public sealed class DiscoveryCatalogueTests
{
    private const string Query = "/api/public/parking-sites?lat=0&lon=0&vehicleType=CAR&limit=1";
    [Fact]
    public async Task RealHostValidatesPaginatesAndDistinguishesOutage()
    {
        using var f = new Factory();
        using var client = f.CreateClient();
        var first = await Data(await client.GetAsync(Query));
        Assert.Empty(first.GetProperty("items").EnumerateArray());
        Assert.NotEqual(Guid.Empty, first.GetProperty("catalogueVersion").GetGuid());
        f.Store.Rows.Add(Row(Guid.Parse("11111111-1111-4111-8111-111111111111")));
        f.Store.Rows.Add(Row(Guid.Parse("22222222-2222-4222-8222-222222222222")));
        first = await Data(await client.GetAsync(Query));
        var cursor = Uri.EscapeDataString(first.GetProperty("nextCursor").GetString()!);
        Assert.Single(first.GetProperty("items").EnumerateArray());
        var next = await Data(await client.GetAsync(Query + "&cursor=" + cursor));
        Assert.Single(next.GetProperty("items").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, next.GetProperty("nextCursor").ValueKind);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(Query.Replace("lat=0","lat=1") + "&cursor=" + cursor)).StatusCode);
        foreach (var query in new[] { "?lat=NaN&lon=0&vehicleType=CAR", "?lat=91&lon=0&vehicleType=CAR",
            "?lat=0&lon=0&vehicleType=CAR&radiusKm=0", "?lat=0&lon=0&vehicleType=ANY", "?lat=0&lon=0&vehicleType=CAR&limit=101",
            "?lon=0&vehicleType=CAR", "?lat=0&lat=1&lon=0&vehicleType=CAR" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/public/parking-sites" + query)).StatusCode);
        f.Store.Version = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Conflict, (await client.GetAsync(Query + "&cursor=" + cursor)).StatusCode);
        f.Store.Fail = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync(Query)).StatusCode);
    }
    [Fact]
    public async Task RealHostPublicationUsesApprovalAndWithdrawalAndRequiresCoordinates()
    {
        using var f = new Factory();
        using var client = f.CreateClient();
        var id = Guid.NewGuid();
        var fields = new PublishSite("Synthetic", "Synthetic address", 0, 0, "outdoor", ["CAR"], null);
        var path = $"/api/parking-lots/{id}/publication";
        f.Authority.Deny = true;
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(path, fields)).StatusCode);
        Assert.Empty(f.Store.Rows);
        f.Authority.Deny = false;
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync(path, fields)).StatusCode);
        var row = Assert.Single((await Data(await client.GetAsync(Query))).GetProperty("items").EnumerateArray());
        Assert.Equal(0, row.GetProperty("latitude").GetDecimal());
        Assert.False(row.TryGetProperty("tenantId", out _));
        Assert.Equal(JsonValueKind.Null, row.GetProperty("serviceWindow").ValueKind);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(path, new
            { name="Synthetic", address="Synthetic address", lotType="outdoor", supportedVehicleTypes=new[] { "CAR" } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(path, fields with { SupportedVehicleTypes=["CAR","CAR"] })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(path)).StatusCode);
        Assert.Empty((await Data(await client.GetAsync(Query))).GetProperty("items").EnumerateArray());
    }
    [Fact]
    public async Task FrameworkErrorsHaveSafeEnvelopes()
    {
        using var f=new Factory();
        using var client=f.CreateClient();
        var path=$"/api/parking-lots/{Guid.NewGuid()}/publication";
        using var getWithBody=new HttpRequestMessage(HttpMethod.Get, Query) { Content=JsonContent.Create(new { location="synthetic" }) };
        foreach (var response in new[] { await client.SendAsync(getWithBody), await client.PostAsync(Query, null),
            await client.PutAsync(path,new StringContent("body")),
            await client.PutAsync(path,new StringContent("{broken",System.Text.Encoding.UTF8,"application/json")) })
        {
            Assert.Contains(response.StatusCode,new[] { HttpStatusCode.BadRequest,HttpStatusCode.MethodNotAllowed,HttpStatusCode.UnsupportedMediaType });
            Assert.Equal("no-store",response.Headers.CacheControl?.ToString());
            var body=await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(body.GetProperty("success").GetBoolean());
        }
    }
    [Fact]
    public async Task SaturatedCatalogueReturns429WithNoStoreAndReleasesPermits()
    {
        using var f=new Factory();
        using var client=f.CreateClient();
        f.Store.Pause=new(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests=Enumerable.Range(0,16).Select(_ => client.GetAsync(Query)).ToArray();
        try
        {
            await f.Store.AtCapacity.Task.WaitAsync(TimeSpan.FromSeconds(5));
            using var busy=await client.GetAsync(Query);
            Assert.Equal(HttpStatusCode.TooManyRequests,busy.StatusCode);
            Assert.Equal("no-store",busy.Headers.CacheControl?.ToString());
            Assert.Equal("RATE_LIMITED",(await busy.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        }
        finally { f.Store.Pause.TrySetResult(true); }
        foreach (var response in await Task.WhenAll(requests)) response.Dispose();
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync(Query)).StatusCode);
    }
    private static CandidateSite Row(Guid id) => new(id, "Synthetic", "Synthetic address", 0, 0, "outdoor", ["CAR"],
        Guid.NewGuid(), DateTimeOffset.UtcNow, null);
    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").Clone();
    }
    private sealed class Authority : IPublicationAuthority
    {
        public bool Deny;
        public Task<ApprovedSite> RequireApprovedSite(Guid id, CancellationToken ct) => Deny
            ? throw new StructureException("SITE_NOT_FOUND", "Approval missing.")
            : Task.FromResult(new ApprovedSite(id, Guid.NewGuid(), "outdoor"));
        public Task<Guid[]> ManagedTenants(CancellationToken ct) => Task.FromResult(new[] { Guid.NewGuid() });
    }
    private sealed class Store : ICatalogueStore
    {
        public List<CandidateSite> Rows = [];
        public Guid Version = Guid.NewGuid();
        public bool Fail;
        public TaskCompletionSource<bool>? Pause;
        public TaskCompletionSource<bool> AtCapacity=new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int waiting;
        public async Task<CatalogueSlice> List(CatalogueFilter filter, CataloguePosition? position, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (Pause != null)
            {
                if (Interlocked.Increment(ref waiting)==16) AtCapacity.TrySetResult(true);
                await Pause.Task.WaitAsync(ct);
            }
            if (Fail) throw new InvalidOperationException("synthetic outage");
            if (position != null && position.CatalogueVersion != Version) throw new StructureException("SEARCH_CHANGED", "Restart.");
            var rows = Rows.OrderBy(x => x.SiteId).Where(x => position == null || x.SiteId.CompareTo(position.LastSiteId) > 0)
                .Take(filter.Limit + 1).ToArray();
            return new CatalogueSlice(rows, Version, DateTimeOffset.UtcNow);
        }
        public Task Publish(ApprovedSite approval, PublishSite fields, CancellationToken ct)
        { Rows.Add(Row(approval.SiteId)); Version=Guid.NewGuid(); return Task.CompletedTask; }
        public Task Withdraw(Guid id, Guid[] tenants, CancellationToken ct)
        { Rows.RemoveAll(x => x.SiteId==id); Version=Guid.NewGuid(); return Task.CompletedTask; }
    }
    private sealed class Factory : WebApplicationFactory<Program>
    {
        public Store Store = new();
        public Authority Authority = new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("Services:Key", "synthetic-only-discovery-service-key");
            builder.UseSetting("ConnectionStrings:Parking", "Host=localhost;Port=1;Database=unused;Username=unused;Password=unused");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICatalogueStore>(); services.AddSingleton<ICatalogueStore>(Store);
                services.RemoveAll<IPublicationAuthority>(); services.AddSingleton<IPublicationAuthority>(Authority);
                var worker=services.FirstOrDefault(s => s.ImplementationType==typeof(StructureSynchronizationWorker));
                if (worker != null) services.Remove(worker);
            });
        }
    }
}
