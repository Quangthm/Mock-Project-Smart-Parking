using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UserService.API;
using UserService.Application.Media;

namespace SmartParking.UserService.Tests;

public sealed class RequestSizeTests
{
    [Theory]
    [InlineData("vehicle", 1_048_576, true)]
    [InlineData("vehicle", 1_048_577, false)]
    [InlineData("avatar", 5_242_880, true)]
    [InlineData("avatar", 5_242_881, false)]
    public void MediatorValidationUsesThePurposeSpecificLimit(string purpose, int size, bool valid)
    {
        var command = new UploadImageCommand(purpose, new byte[size], "key", null,
            purpose == "vehicle" ? Guid.NewGuid() : null);
        Assert.Equal(valid, new UploadImageValidator().Validate(command).IsValid);
    }

    [Fact]
    public async Task KestrelEnforcesOrdinaryAndRoutedBodyLimitsAndCors()
    {
        const string origin = "http://frontend.test";
        using var key = RSA.Create(2048);
        await using var app = await UserHost.BuildAsync(["--environment", "Testing"], builder =>
        {
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Logging.ClearProviders();
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:User"] = "Host=localhost;Database=unused;Username=unused",
                ["Services:Parking"] = "http://parking.test/", ["Services:Key"] = new string('x', 40),
                ["Messaging:Enabled"] = "false", ["Cors:AllowedOrigins"] = origin,
                ["Jwt:Issuer"] = "size-tests", ["Jwt:Audience"] = "size-tests",
                ["Jwt:PrivateKeyPem"] = key.ExportRSAPrivateKeyPem()
            });
        }, builder => builder.Services.RemoveAll<IHostedService>());
        app.MapPost("/size-test", async (HttpRequest request) => await request.Body.CopyToAsync(Stream.Null));
        // Isolate MVC form binding and transport from identity and provider IO.
        // Successful dispatch reaches the deliberately disabled media handler.
        app.MapControllers().AllowAnonymous();
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        foreach (var chunked in new[] { false, true })
        {
            foreach (var size in new[] { 64_000, 65_536, 65_537 })
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "/size-test") { Content = new ByteArrayContent(new byte[size]) };
                request.Headers.TransferEncodingChunked = chunked;
                request.Headers.Add("Origin", origin);
                using var response = await client.SendAsync(request);
                // Kestrel counts HTTP/1.1 chunk framing toward the transport limit.
                var oversized = size > 65_536 || chunked && size == 65_536;
                Assert.Equal(oversized ? HttpStatusCode.RequestEntityTooLarge : HttpStatusCode.OK, response.StatusCode);
                Assert.Equal(origin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
                if (oversized) await AssertCode(response, "PAYLOAD_TOO_LARGE");
            }
            foreach (var (path, limit) in new[] { ("/api/vehicles/images", 1_048_576), ("/api/auth/me/avatar/images", 5_242_880) })
            {
                foreach (var excess in new[] { 0, 1, 65_536 })
                {
                    var file = MediaAssetTests.JpegOfSize(limit + excess);
                    using var form = new MultipartFormDataContent();
                    form.Add(new ByteArrayContent(file), "file", "image.jpg");
                    if (path.Contains("vehicles")) form.Add(new StringContent(Guid.NewGuid().ToString()), "draftIntentId");
                    using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = form };
                    request.Headers.TransferEncodingChunked = chunked;
                    // Let the server return its declared-length rejection before
                    // the client races to send a multi-megabyte body.
                    request.Headers.ExpectContinue = true;
                    request.Headers.Add("Origin", origin);
                    request.Headers.Add("Idempotency-Key", "size-test");
                    using var response = await client.SendAsync(request);
                    var expected = excess switch { 0 => HttpStatusCode.ServiceUnavailable, 1 => HttpStatusCode.BadRequest, _ => HttpStatusCode.RequestEntityTooLarge };
                    Assert.True(expected == response.StatusCode, $"{path}, chunked={chunked}, excess={excess}: expected {expected}, received {response.StatusCode}");
                    Assert.Equal(origin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
                    await AssertCode(response, excess switch { 0 => "MEDIA_DISABLED", 1 => "MEDIA_INVALID_IMAGE", _ => "PAYLOAD_TOO_LARGE" });
                }
            }
        }
        using var foreign = new HttpRequestMessage(HttpMethod.Post, "/size-test") { Content = new ByteArrayContent(new byte[65_537]) };
        foreign.Headers.Add("Origin", "http://other.test");
        using var rejection = await client.SendAsync(foreign);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, rejection.StatusCode);
        Assert.False(rejection.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private static async Task AssertCode(HttpResponseMessage response, string code) =>
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
}
