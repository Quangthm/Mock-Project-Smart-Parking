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

internal sealed class SearchTestHost : IAsyncDisposable
{
    public const string Secret = "SYNTHETIC-PAYMENT-KEY-TEST-ONLY-0123456";
    public SearchTestClock Clock = new();
    public SearchTestOwners Owners = null!;
    public SearchTestCapacity? CapacitySource;
    public SearchTestLogs Logs = new();
    public HttpClient Client = null!;
    private WebApplication app = null!;
    public static async Task<SearchTestHost> Start(bool enabled = true, string? capacityMode = null, bool ephemeralProtection = true, bool validateServices = false)
    {
        var f = new SearchTestHost();
        f.Owners = new(f.Clock, capacityMode);
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        f.app = OperationHost.Build([], b =>
        {
            if (validateServices) b.Host.UseDefaultServiceProvider(options =>
            {
                options.ValidateOnBuild = true;
                options.ValidateScopes = true;
            });
            b.Configuration.Sources.Clear();
            b.Configuration.AddInMemoryCollection(new Dictionary<string,string?> {
                ["Services:Key"] = new('k', 32), ["ConnectionStrings:Reservation"] = "Host=synthetic.invalid;Database=synthetic",
                ["ReservationIntegration:OperationsPrivateKeyPem"] = rsa.ExportRSAPrivateKeyPem(),
                ["Discovery:Enabled"] = enabled.ToString(), ["Discovery:FacilityOrigin"] = "https://facility.invalid",
                ["Discovery:PaymentOrigin"] = "https://payment.invalid", ["Discovery:PaymentServiceKey"] = Secret,
                ["Discovery:MaxAgeSeconds"] = "60", ["Discovery:ClockSkewSeconds"] = "1"
            });
            b.WebHost.UseTestServer();
            b.Services.AddSingleton<TimeProvider>(f.Clock);
            if (capacityMode is not null)
            {
                f.CapacitySource = new SearchTestCapacity(f.Clock, capacityMode);
                b.Host.ConfigureContainer<IServiceCollection>((_, services) => services.AddSingleton<IDiscoveryCapacityReader>(f.CapacitySource));
            }
            if (ephemeralProtection) b.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
            b.Logging.AddProvider(f.Logs);
            foreach (var name in new[] { "discovery-facility", "discovery-payment" })
                b.Services.PostConfigure<HttpClientFactoryOptions>(name, o => o.HttpMessageHandlerBuilderActions.Add(x => x.PrimaryHandler = f.Owners));
        });
        f.app.Configuration["ReservationIntegration:Enabled"] = "true";
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
