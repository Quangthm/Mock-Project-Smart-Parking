using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using SmartParking.PaymentService.API.Authentication;

namespace SmartParking.PaymentService.Tests;

/// <summary>
/// SPARK-340 Round 2 hardening (finding 3.1) coverage for the UserServiceJwt scheme:
/// restricted audiences/issuers, mandatory actor claims, explicit local opt-in for
/// insecure HTTP JWKS and key-dependency outage distinguished as 503.
/// </summary>
public class UserServiceJwtAuthenticationTests
{
    private const string Issuer = "SmartParking.UserService";
    private const string Audience = "SmartParking";
    private const string DemoAudience = "SmartParking.Postman.Demo";
    private const string Scheme = UserServiceJwtAuthentication.SchemeName;

    private static IConfiguration Configuration(IDictionary<string, string?> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

    private static IHost BuildHost(IDictionary<string, string?> settings)
    {
        var configuration = Configuration(settings);

        var builder = new HostBuilder().ConfigureWebHost(web => web
            .UseTestServer()
            .UseEnvironment("Production")
            .ConfigureServices(services =>
            {
                services.AddSingleton<IConfiguration>(configuration);
                services.AddRouting();
                services.AddAuthorization();
                services.AddLogging();
                services.AddUserServiceJwtAuthentication(configuration);
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/secure", () => Results.Ok(new { status = "ok" }))
                        .RequireAuthorization(new AuthorizeAttribute
                        {
                            AuthenticationSchemes = Scheme,
                            Roles = "ADMIN"
                        });
                });
            }));

        return builder.StartAsync().GetAwaiter().GetResult();
    }

    private static IHost BuildPemHost(RSA rsa) => BuildHost(new Dictionary<string, string?>
    {
        ["JwtValidation:Issuer"] = Issuer,
        ["JwtValidation:Audience"] = Audience,
        ["JwtValidation:PublicKeyPem"] = rsa.ExportSubjectPublicKeyInfoPem()
    });

    [Theory]
    [InlineData("not-a-guid", true, null, false)]
    [InlineData("00000000-0000-0000-0000-000000000000", true, null, false)]
    [InlineData("11111111-1111-4111-8111-111111111111", false, null, false)]
    [InlineData("11111111-1111-4111-8111-111111111111", true, "not-a-guid", false)]
    [InlineData("11111111-1111-4111-8111-111111111111", true, null, true)]
    public async Task TrustedToken_WithMalformedActorContract_IsRejected(
        string sub, bool includeAccount, string? accountId, bool duplicateRole)
    {
        using var rsa = RSA.Create(2048);
        using var host = BuildPemHost(rsa);
        using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            MintToken(rsa, sub, "ADMIN", includeAccount: includeAccount,
                accountId: accountId, duplicateRole: duplicateRole));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/secure")).StatusCode);
    }

    private static string MintToken(RSA rsa, string? sub, string? role,
        string audience = Audience, string issuer = Issuer,
        bool includeAccount = true, string? accountId = null, bool duplicateRole = false)
    {
        var claims = new List<Claim>();
        if (sub is not null) claims.Add(new Claim("sub", sub));
        if (role is not null) claims.Add(new Claim("role", role));
        if (includeAccount) claims.Add(new Claim("account_id", accountId ?? Guid.NewGuid().ToString()));
        if (duplicateRole) claims.Add(new Claim("role", "driver"));

        var handler = new JwtSecurityTokenHandler();
        handler.InboundClaimTypeMap.Clear();
        var token = handler.CreateJwtSecurityToken(
            issuer: issuer,
            audience: audience,
            subject: new ClaimsIdentity(claims),
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256));
        return handler.WriteToken(token);
    }

    // ---------------------------------------------------------------------
    // 3.1.3 Mandatory actor claims.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task MissingSub_Returns401()
    {
        using var rsa = RSA.Create(2048);
        using var host = BuildPemHost(rsa);
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", MintToken(rsa, sub: null, role: "ADMIN"));

        var response = await client.GetAsync("/secure");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MissingRole_Returns401()
    {
        using var rsa = RSA.Create(2048);
        using var host = BuildPemHost(rsa);
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", MintToken(rsa, sub: Guid.NewGuid().ToString(), role: null));

        var response = await client.GetAsync("/secure");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task BlankSub_Returns401()
    {
        using var rsa = RSA.Create(2048);
        using var host = BuildPemHost(rsa);
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", MintToken(rsa, sub: "   ", role: "ADMIN"));

        var response = await client.GetAsync("/secure");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ValidActorClaims_Returns200()
    {
        using var rsa = RSA.Create(2048);
        using var host = BuildPemHost(rsa);
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", MintToken(rsa, sub: Guid.NewGuid().ToString(), role: "ADMIN"));

        var response = await client.GetAsync("/secure");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---------------------------------------------------------------------
    // 3.1.1 Audience restriction (no demo trust).
    // ---------------------------------------------------------------------

    [Fact]
    public async Task DemoAudience_Returns401()
    {
        using var rsa = RSA.Create(2048);
        using var host = BuildPemHost(rsa);
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", MintToken(rsa, sub: Guid.NewGuid().ToString(), role: "ADMIN", audience: DemoAudience));

        var response = await client.GetAsync("/secure");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OfficialAudience_Returns200()
    {
        using var rsa = RSA.Create(2048);
        using var host = BuildPemHost(rsa);
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", MintToken(rsa, sub: Guid.NewGuid().ToString(), role: "ADMIN", audience: Audience));

        var response = await client.GetAsync("/secure");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DemoIssuer_Returns401()
    {
        using var rsa = RSA.Create(2048);
        using var host = BuildPemHost(rsa);
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", MintToken(rsa, sub: Guid.NewGuid().ToString(), role: "ADMIN", issuer: "SmartParking.UserService.Demo"));

        var response = await client.GetAsync("/secure");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------------------------------------------------------------------
    // 3.1.2 Explicit local opt-in for insecure HTTP JWKS.
    // ---------------------------------------------------------------------

    [Fact]
    public void HttpJwksUri_WithoutOptIn_InProduction_Throws()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Production",
            ["JwtValidation:JwksUri"] = "http://jwks.example.com/.well-known/jwks.json"
        });
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() => services.AddUserServiceJwtAuthentication(configuration));
    }

    [Fact]
    public void HttpJwksUri_WithOptInExplicitlyFalse_InProduction_Throws()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Production",
            ["JwtValidation:JwksUri"] = "http://jwks.example.com/.well-known/jwks.json",
            ["JwtValidation:AllowInsecureTransport"] = "false"
        });
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() => services.AddUserServiceJwtAuthentication(configuration));
    }

    [Fact]
    public void HttpJwksUri_WithExplicitOptIn_IsAccepted()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["JwtValidation:JwksUri"] = "http://127.0.0.1:1/.well-known/jwks.json",
            ["JwtValidation:AllowInsecureTransport"] = "true"
        });
        var services = new ServiceCollection();

        services.AddLogging();
        var exception = Record.Exception(() => services.AddUserServiceJwtAuthentication(configuration));

        Assert.Null(exception);
    }

    [Fact]
    public void HttpJwksUri_Loopback_InDevelopment_IsAcceptedWithoutFlag()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            ["JwtValidation:JwksUri"] = "http://127.0.0.1:5080/.well-known/jwks.json"
        });
        var services = new ServiceCollection();

        services.AddLogging();
        var exception = Record.Exception(() => services.AddUserServiceJwtAuthentication(configuration));

        Assert.Null(exception);
    }

    [Fact]
    public void HttpsJwksUri_IsAccepted_InProduction()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Production",
            ["JwtValidation:JwksUri"] = "https://jwks.example.com/.well-known/jwks.json"
        });
        var services = new ServiceCollection();

        services.AddLogging();
        var exception = Record.Exception(() => services.AddUserServiceJwtAuthentication(configuration));

        Assert.Null(exception);
    }

    // ---------------------------------------------------------------------
    // 3.1.4 JWKS outage (503, distinct code) vs bad token (401).
    // ---------------------------------------------------------------------

    [Fact]
    public async Task JwksOutage_Returns503WithDistinctCode()
    {
        using var host = BuildHost(new Dictionary<string, string?>
        {
            ["JwtValidation:Issuer"] = Issuer,
            ["JwtValidation:Audience"] = Audience,
            ["JwtValidation:JwksUri"] = "http://127.0.0.1:1/.well-known/jwks.json"
        });
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "any.token.value");

        var response = await client.GetAsync("/secure");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("AUTHENTICATION_KEYS_UNAVAILABLE", body);
        Assert.Contains("\"success\":false", body);
    }

    [Fact]
    public async Task BadSignatureToken_Returns401Not503()
    {
        using var rsa = RSA.Create(2048);
        using var host = BuildPemHost(rsa);
        var client = host.GetTestClient();

        using var foreignRsa = RSA.Create(2048);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", MintToken(foreignRsa, sub: Guid.NewGuid().ToString(), role: "ADMIN"));

        var response = await client.GetAsync("/secure");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("AUTHENTICATION_KEYS_UNAVAILABLE", body);
    }
}
