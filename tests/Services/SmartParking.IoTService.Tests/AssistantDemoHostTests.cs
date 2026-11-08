using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.IoTService.API;
using SmartParking.IoTService.Application.Assistant.Authorization;
using SmartParking.IoTService.Application.Assistant.Demo;

namespace SmartParking.IoTService.Tests;

public sealed class AssistantDemoHostTests
{
    [Theory]
    [InlineData("Production", "true")]
    [InlineData("Development", "true")]
    [InlineData("Testing", "true")]
    [InlineData("Demo", "false")]
    [InlineData("Demo", "")]
    public void DemoRequiresBothExplicitEnvironmentAndOptIn(string environment, string enabled)
    {
        Assert.ThrowsAny<InvalidOperationException>(() => Build(environment, enabled));
    }

    [Theory]
    [InlineData("*")]
    [InlineData("https://*.example.test")]
    [InlineData("https://example.test/path")]
    [InlineData("https://example.test/")]
    [InlineData("ftp://example.test")]
    [InlineData("https://user:password@example.test")]
    public void InvalidCorsOriginDoesNotStart(string origin)
    {
        Assert.Throws<InvalidOperationException>(() => Build(origin: origin));
    }

    [Fact]
    public async Task HealthAndCapabilitiesAreClearlyDemoWithoutActorOrLiveFeatures()
    {
        await using var app = Build();
        await app.StartAsync();
        using var client = app.GetTestClient();
        Assert.Null(app.Services.GetService<ICurrentActor>());
        Assert.NotNull(app.Services.GetService<IDemoAssistantStatusProvider>());
        using var live = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.NoContent, live.StatusCode);
        Assert.Empty(await live.Content.ReadAsByteArrayAsync());
        Assert.Equal("demo", live.Headers.GetValues("X-Assistant-Mode").Single());
        using var ready = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        var readiness = await Body(ready);
        Assert.True(readiness.GetProperty("data").GetProperty("isDemo").GetBoolean());
        Assert.Equal("demo-api-only", readiness.GetProperty("data").GetProperty("readyFor").GetString());
        using var response = await client.GetAsync("/api/demo/assistant/capabilities");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await Body(response);
        var data = body.GetProperty("data");
        Assert.True(data.GetProperty("isDemo").GetBoolean());
        Assert.Contains("demo", data.GetProperty("notice").GetString());
        Assert.False(data.GetProperty("capabilities").GetProperty("enabled").GetBoolean());
        Assert.All(data.GetProperty("capabilities").GetProperty("features").EnumerateObject(), feature => Assert.False(feature.Value.GetBoolean()));
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal(body.GetProperty("traceId").GetString(), response.Headers.GetValues("X-Correlation-ID").Single());
    }

    [Theory]
    [InlineData("/api/assistant/capabilities", "GET", 404, "RESOURCE_NOT_FOUND")]
    [InlineData("/api/assistant/conversations", "POST", 404, "RESOURCE_NOT_FOUND")]
    [InlineData("/api/demo/assistant/capabilities?role=admin", "GET", 400, "VALIDATION_FAILED")]
    [InlineData("/api/demo/assistant/capabilities", "POST", 405, "METHOD_NOT_ALLOWED")]
    public async Task DemoDoesNotExposeProductionRoutesOrAcceptRoleOverrides(string path, string method, int status, string code)
    {
        await using var app = Build();
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Add("X-Role", "admin");
        using var response = await client.SendAsync(request);
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal(code, (await Body(response)).GetProperty("code").GetString());
        Assert.Equal("demo", response.Headers.GetValues("X-Assistant-Mode").Single());
        if (status == 405) Assert.Contains("GET", response.Content.Headers.Allow);
    }

    [Theory]
    [InlineData("https://frontend.example.test", true)]
    [InlineData("https://untrusted.example.test", false)]
    public async Task BrowserOriginMustBeExplicitlyAllowed(string origin, bool allowed)
    {
        await using var app = Build(origin: "https://frontend.example.test");
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/demo/assistant/capabilities");
        request.Headers.Add("Origin", origin);
        using var response = await client.SendAsync(request);
        Assert.Equal(allowed, response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
        if (allowed)
        {
            Assert.Equal(origin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
            Assert.Contains("X-Assistant-Mode", response.Headers.GetValues("Access-Control-Expose-Headers").Single());
        }
    }

    [Fact]
    public async Task CorsDefaultsToNoCrossOriginGrantAndPreflightOnlyAllowsGet()
    {
        await using var app = Build();
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/demo/assistant/capabilities");
        request.Headers.Add("Origin", "https://frontend.example.test");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        using var response = await client.SendAsync(request);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));

        await using var configured = Build(origin: "https://frontend.example.test");
        await configured.StartAsync();
        using var configuredClient = configured.GetTestClient();
        using var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/demo/assistant/capabilities");
        preflight.Headers.Add("Origin", "https://frontend.example.test");
        preflight.Headers.Add("Access-Control-Request-Method", "GET");
        using var granted = await configuredClient.SendAsync(preflight);
        Assert.Equal(HttpStatusCode.NoContent, granted.StatusCode);
        Assert.Contains("GET", granted.Headers.GetValues("Access-Control-Allow-Methods").Single());
        Assert.Contains("POST", granted.Headers.GetValues("Access-Control-Allow-Methods").Single());
    }

    private static WebApplication Build(string environment = "Demo", string enabled = "true", string? origin = null) =>
        AssistantDemoHost.Build(["--environment", environment], builder =>
        {
            builder.WebHost.UseTestServer();
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Assistant:Demo:Enabled"] = enabled,
                ["Assistant:Demo:AllowedOrigins:0"] = origin
            }.Where(pair => pair.Value is not null));
        });

    private static async Task<JsonElement> Body(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}
