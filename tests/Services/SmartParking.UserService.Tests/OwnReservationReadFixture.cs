using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SmartParking.OperationService.API;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Application.DTOs;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Domain.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace SmartParking.UserService.Tests;

internal sealed class OwnReservationReadFixture : IAsyncDisposable
{
    public OwnReservationReadClock Clock = new(); public OwnReservationReadStore Store = new(); public HttpClient Client = null!;
    private WebApplication app = null!; private readonly RSA key = RSA.Create(2048);
    public static async Task<OwnReservationReadFixture> Create(bool enabled = true)
    {
        var f = new OwnReservationReadFixture();
        f.app = OperationHost.Build(["--environment", "Testing"], b => {
            b.Configuration.Sources.Clear(); b.WebHost.UseTestServer();
            b.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
                ["Services:Key"] = new('x', 32), ["ConnectionStrings:Reservation"] = "Host=synthetic.invalid;Database=synthetic",
                ["ReservationAdmission:Enabled"] = enabled.ToString(), ["ReservationAdmission:BrokerPassword"] = "synthetic-only",
                ["JwtValidation:Issuer"] = "synthetic-user", ["JwtValidation:Audience"] = "synthetic-business",
                ["JwtValidation:JwksUri"] = "https://synthetic.invalid/jwks"
            });
            // Replace external I/O only; exercise real routes, JWT middleware, actor, validators and handlers.
            b.Host.ConfigureContainer<IServiceCollection>((_, services) => {
                foreach (var hosted in services.Where(x => x.ServiceType == typeof(IHostedService) &&
                    x.ImplementationType?.FullName != "Microsoft.AspNetCore.Hosting.GenericWebHostService").ToArray()) services.Remove(hosted);
                services.RemoveAll<IReservationBookingRepository>();
                services.AddSingleton<IReservationBookingRepository>(f.Store);
                if (enabled) services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options => {
                    var config = new OpenIdConnectConfiguration { Issuer = "synthetic-user" };
                    config.SigningKeys.Add(new RsaSecurityKey(f.key.ExportParameters(false)));
                    options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(config);
                });
            });

        });
        await f.app.StartAsync(); f.Client = f.app.GetTestClient(); return f;
    }
    public string Token(Guid account, Guid user, string role = "driver", bool expired = false, string audience = "synthetic-business")
    {
        var header = Base64UrlEncoder.Encode("{\"alg\":\"RS256\",\"typ\":\"JWT\"}");
        var payload = Base64UrlEncoder.Encode(JsonSerializer.Serialize(new { iss = "synthetic-user", aud = audience, sub = user, account_id = account, jti = Guid.NewGuid().ToString("N"), role, nbf = Clock.Now.AddMinutes(-10).ToUnixTimeSeconds(), exp = Clock.Now.AddMinutes(expired ? -5 : 15).ToUnixTimeSeconds() }));
        var body = header + "." + payload; return body + "." + Base64UrlEncoder.Encode(key.SignData(Encoding.ASCII.GetBytes(body), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }
    public Task<HttpResponseMessage> Get(string path, string token)
    { var request = new HttpRequestMessage(HttpMethod.Get, path); if (token != "") request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token); return Client.SendAsync(request); }
    public async ValueTask DisposeAsync() { Client.Dispose(); await app.StopAsync(); await app.DisposeAsync(); key.Dispose(); }
}
