using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using SmartParking.PaymentService.API.Authentication;
using SmartParking.PaymentService.API.Middleware;
using SmartParking.PaymentService.Application.Common.Interfaces;
using SmartParking.PaymentService.Infrastructure.Adapters;

namespace SmartParking.PaymentService.Tests.Authentication;

public sealed class UserJwtAuthenticationTests
{
    [Fact]
    public async Task DevelopmentPolicyScheme_RoutesRealJwtToUserJwt_AndResolvesRealAccountId()
    {
        using var rsa = RSA.Create(2048);
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(rsa) { KeyId = "test-kid" });
        jwk.Use = "sig";
        jwk.Alg = "RS256";
        var jwksJson = JsonSerializer.Serialize(new
        {
            keys = new[]
            {
                new
                {
                    kty = jwk.Kty,
                    use = jwk.Use,
                    alg = jwk.Alg,
                    kid = jwk.Kid,
                    n = jwk.N,
                    e = jwk.E
                }
            }
        });

        await using var jwksServer = WebApplication.CreateBuilder().Build();
        jwksServer.Urls.Add("http://127.0.0.1:0");
        jwksServer.MapGet("/.well-known/jwks.json", () => Results.Text(jwksJson, "application/json"));
        await jwksServer.StartAsync();
        var jwksUri = $"{jwksServer.Urls.First()}/.well-known/jwks.json";

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JwtValidation:Issuer"] = "SmartParking.UserService",
            ["JwtValidation:Audience"] = "SmartParking",
            ["JwtValidation:JwksUri"] = jwksUri,
            ["JwtValidation:AllowInsecureTransport"] = "true"
        });

        const string devOrJwtScheme = "SmartParkingDevOrJwt";
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentActor, CurrentActorAdapter>();
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = devOrJwtScheme;
            options.DefaultChallengeScheme = devOrJwtScheme;
        })
        .AddPolicyScheme(devOrJwtScheme, devOrJwtScheme, options =>
        {
            options.ForwardDefaultSelector = context =>
            {
                if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
                {
                    var header = authHeader.ToString();
                    if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        var token = header["Bearer ".Length..].Trim();
                        if (token.Count(c => c == '.') == 2)
                        {
                            return UserJwtAuthentication.Scheme;
                        }
                    }
                }

                return DevAuthenticationHandler.SchemeName;
            };
        })
        .AddScheme<AuthenticationSchemeOptions, DevAuthenticationHandler>(DevAuthenticationHandler.SchemeName, null);

        builder.Services.AddUserJwtAuthentication(builder.Configuration, builder.Environment);

        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/whoami", (ICurrentActor actor) => Results.Ok(new
        {
            actor.IsAuthenticated,
            actor.AccountId,
            actor.Role
        })).RequireAuthorization();

        await app.StartAsync();
        var client = app.GetTestClient();

        var driverId = Guid.Parse("77777777-8888-9999-aaaa-bbbbbbbbbbbb");
        var jwt = IssueDriverJwt(rsa, "test-kid", driverId);

        using var jwtRequest = new HttpRequestMessage(HttpMethod.Get, "/whoami");
        jwtRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        var jwtResponse = await client.SendAsync(jwtRequest);
        Assert.Equal(HttpStatusCode.OK, jwtResponse.StatusCode);

        var jwtBody = await JsonDocument.ParseAsync(await jwtResponse.Content.ReadAsStreamAsync());
        Assert.Equal(driverId, jwtBody.RootElement.GetProperty("accountId").GetGuid());

        using var devRequest = new HttpRequestMessage(HttpMethod.Get, "/whoami");
        devRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "driver-token");
        var devResponse = await client.SendAsync(devRequest);
        Assert.Equal(HttpStatusCode.OK, devResponse.StatusCode);

        var devBody = await JsonDocument.ParseAsync(await devResponse.Content.ReadAsStreamAsync());
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), devBody.RootElement.GetProperty("accountId").GetGuid());
    }

    [Fact]
    public void HttpJwksWithoutAllowInsecureTransport_InDevelopment_Throws()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtValidation:Issuer"] = "SmartParking.UserService",
                ["JwtValidation:Audience"] = "SmartParking",
                ["JwtValidation:JwksUri"] = "http://user-service:8080/.well-known/jwks.json"
            })
            .Build();

        var services = new ServiceCollection();
        var env = new FakeHostEnvironment { EnvironmentName = Environments.Development };

        Assert.Throws<InvalidOperationException>(() =>
            services.AddUserJwtAuthentication(configuration, env));
    }

    private static string IssueDriverJwt(RSA rsa, string kid, Guid accountId)
    {
        var key = new RsaSecurityKey(rsa) { KeyId = kid };
        var credentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
        var now = DateTime.UtcNow;

        var token = new JwtSecurityToken(
            issuer: "SmartParking.UserService",
            audience: "SmartParking",
            claims:
            [
                new Claim("sub", accountId.ToString()),
                new Claim("account_id", accountId.ToString()),
                new Claim("role", "DRIVER")
            ],
            notBefore: now.AddMinutes(-1),
            expires: now.AddMinutes(5),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "SmartParking.PaymentService.API";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
