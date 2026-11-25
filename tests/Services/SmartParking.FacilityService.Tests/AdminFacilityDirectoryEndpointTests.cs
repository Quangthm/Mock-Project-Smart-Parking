using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using SmartParking.FacilityService.API;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Infrastructure;

namespace SmartParking.FacilityService.Tests;

public sealed class AdminFacilityDirectoryEndpointTests
{
    private const string Route = "/api/v1/admin/facility-directory";

    [Fact]
    public async Task AdminReceivesNamedTenantAndSiteOptionsThroughRealResolverAndMediator()
    {
        var store = new AdminFacilityDirectoryTestStore();
        var calls = new ConcurrentQueue<string>();
        using var factory = CreateFactory(store, calls);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "admin");
        using var response = await client.GetAsync(Route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("success").GetBoolean());
        var data = body.GetProperty("data");
        var tenant = Assert.Single(data.GetProperty("tenants").EnumerateArray());
        var site = Assert.Single(data.GetProperty("sites").EnumerateArray());
        Assert.Equal("SmartPark Demo Owner", tenant.GetProperty("name").GetString());
        Assert.Equal("DEMO-OWNER", tenant.GetProperty("code").GetString());
        Assert.Equal("ACTIVE", tenant.GetProperty("status").GetString());
        Assert.Equal("SmartPark Demo Site", site.GetProperty("name").GetString());
        Assert.Equal("DEMO-HQ", site.GetProperty("code").GetString());
        Assert.Equal(tenant.GetProperty("id").GetGuid(), site.GetProperty("tenantId").GetGuid());
        Assert.True(site.GetProperty("isActive").GetBoolean());
        Assert.False(tenant.TryGetProperty("email", out _));
        Assert.False(tenant.TryGetProperty("phone", out _));
        Assert.Equal(1, store.Reads);
        Assert.Equal(new[] { "/api/auth/me" }, calls.ToArray());
    }

    [Theory]
    [InlineData(null, 401, "UNAUTHENTICATED")]
    [InlineData("owner", 403, "FORBIDDEN")]
    [InlineData("driver", 403, "FORBIDDEN")]
    [InlineData("operator", 403, "FORBIDDEN")]
    [InlineData("invalid", 401, "UNAUTHENTICATED")]
    [InlineData("outage", 503, "IDENTITY_UNAVAILABLE")]
    public async Task RejectedCallerNeverReadsDirectory(string? role, int status, string code)
    {
        var store = new AdminFacilityDirectoryTestStore();
        var calls = new ConcurrentQueue<string>();
        using var factory = CreateFactory(store, calls);
        using var client = factory.CreateClient();
        if (role is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", role);
        using var response = await client.GetAsync(Route);
        Assert.Equal(status, (int)response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.Equal(0, store.Reads);
        if (role is null) Assert.Empty(calls);
        else Assert.Contains("/api/auth/me", calls);
    }

    private static WebApplicationFactory<Program> CreateFactory(
        AdminFacilityDirectoryTestStore store, ConcurrentQueue<string> calls)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Services:Key", "admin-facility-directory-tests-service-key");
            builder.UseSetting("Services:User", "http://identity.test/");
            builder.UseSetting("Services:Reservation", "http://reservation.test/");
            builder.UseSetting("ConnectionStrings:Parking",
                "Host=127.0.0.1;Port=1;Database=unused;Username=unused;Password=unused;Timeout=1;Pooling=false");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IFacilityDirectoryStore>();
                services.AddSingleton<IFacilityDirectoryStore>(store);
                services.PostConfigureAll<HttpClientFactoryOptions>(options =>
                    options.HttpMessageHandlerBuilderActions.Add(handler =>
                        handler.PrimaryHandler = new AdminFacilityDirectoryIdentityHandler(calls)));
                var worker = services.FirstOrDefault(descriptor =>
                    descriptor.ImplementationType == typeof(StructureSynchronizationWorker));
                if (worker is not null) services.Remove(worker);
            });
        });
}
