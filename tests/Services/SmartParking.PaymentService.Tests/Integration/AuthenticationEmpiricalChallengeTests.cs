using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace SmartParking.PaymentService.Tests.Integration;

public class AuthenticationEmpiricalChallengeTests
{
    private readonly string _issuer = "SmartParking.UserService";
    private readonly string _audience = "SmartParking";
    private readonly string _symmetricSecret = "smartpark-default-insecure-dev-jwt-key-replace-in-production-min-32-chars";
    private readonly RSA _rsa;
    private readonly string _rsaPublicKeyPem;
    private readonly string _rsaPrivateKeyPem;

    public AuthenticationEmpiricalChallengeTests()
    {
        _rsa = RSA.Create(2048);
        _rsaPrivateKeyPem = _rsa.ExportPkcs8PrivateKeyPem();
        _rsaPublicKeyPem = _rsa.ExportSubjectPublicKeyInfoPem();
    }

    private async Task<IHost> CreateHostAsync(bool includeRsa = true, bool includeSymmetric = true)
    {
        var builder = new HostBuilder().ConfigureWebHost(web => web.UseTestServer()
            .UseEnvironment("Production")
            .ConfigureServices(services =>
            {
                services.AddRouting();

                // Replicate exact Program.cs configuration
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    var signingKeys = new List<SecurityKey>();
                    if (includeRsa)
                    {
                        var rsa = RSA.Create();
                        rsa.ImportFromPem(_rsaPublicKeyPem);
                        signingKeys.Add(new RsaSecurityKey(rsa));
                    }
                    if (includeSymmetric)
                    {
                        signingKeys.Add(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_symmetricSecret)));
                    }

                    options.MapInboundClaims = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = _issuer,
                        ValidateAudience = true,
                        ValidAudience = _audience,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromMinutes(1),
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKeys = signingKeys,
                        IssuerSigningKey = signingKeys.FirstOrDefault(),
                        RoleClaimType = "role",
                        NameClaimType = "sub"
                    };
                });

                services.AddAuthorization();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/api/secure", [Authorize] (ClaimsPrincipal user) =>
                    {
                        var sub = user.FindFirst("sub")?.Value;
                        var role = user.FindFirst("role")?.Value;
                        var isDriver = user.IsInRole("DRIVER");
                        var isAdmin = user.IsInRole("ADMIN");
                        return Results.Ok(new { sub, role, isDriver, isAdmin });
                    });

                    endpoints.MapGet("/api/secure/driver-only", [Authorize(Roles = "DRIVER")] (ClaimsPrincipal user) =>
                    {
                        return Results.Ok(new { message = "driver-ok" });
                    });
                });
            }));

        return await builder.StartAsync();
    }

    private string GenerateSymmetricToken(
        string? issuer = null,
        string? audience = null,
        string? sub = "11111111-1111-1111-1111-111111111111",
        string role = "DRIVER",
        DateTime? expires = null,
        string? secret = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret ?? _symmetricSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var tokenHandler = new JwtSecurityTokenHandler();
        tokenHandler.InboundClaimTypeMap.Clear();

        var notBefore = expires.HasValue ? expires.Value.AddMinutes(-10) : DateTime.UtcNow;
        var token = tokenHandler.CreateJwtSecurityToken(
            issuer: issuer ?? _issuer,
            audience: audience ?? _audience,
            subject: new ClaimsIdentity(new[]
            {
                new Claim("sub", sub ?? Guid.NewGuid().ToString()),
                new Claim("role", role)
            }),
            notBefore: notBefore,
            expires: expires ?? DateTime.UtcNow.AddMinutes(30),
            signingCredentials: creds
        );
        return tokenHandler.WriteToken(token);
    }

    private string GenerateRsaToken(
        string? issuer = null,
        string? audience = null,
        string? sub = "22222222-2222-2222-2222-222222222222",
        string role = "DRIVER",
        DateTime? expires = null,
        RSA? rsa = null)
    {
        var key = new RsaSecurityKey(rsa ?? _rsa);
        var creds = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
        var tokenHandler = new JwtSecurityTokenHandler();
        tokenHandler.InboundClaimTypeMap.Clear();

        var notBefore = expires.HasValue ? expires.Value.AddMinutes(-10) : DateTime.UtcNow;
        var token = tokenHandler.CreateJwtSecurityToken(
            issuer: issuer ?? _issuer,
            audience: audience ?? _audience,
            subject: new ClaimsIdentity(new[]
            {
                new Claim("sub", sub ?? Guid.NewGuid().ToString()),
                new Claim("role", role)
            }),
            notBefore: notBefore,
            expires: expires ?? DateTime.UtcNow.AddMinutes(30),
            signingCredentials: creds
        );
        return tokenHandler.WriteToken(token);
    }

    [Fact]
    public async Task MissingToken_Returns401Unauthorized_WithoutCrashingRuntime()
    {
        using var server = await CreateHostAsync();
        var client = server.GetTestClient();

        var response = await client.GetAsync("/api/secure");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task EmptyBearerHeader_Returns401Unauthorized()
    {
        using var server = await CreateHostAsync();
        var client = server.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "");

        var response = await client.GetAsync("/api/secure");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MalformedToken_Returns401Unauthorized()
    {
        using var server = await CreateHostAsync();
        var client = server.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.valid.jwt.token");

        var response = await client.GetAsync("/api/secure");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredSymmetricToken_Returns401Unauthorized()
    {
        using var server = await CreateHostAsync();
        var client = server.GetTestClient();
        var expiredToken = GenerateSymmetricToken(expires: DateTime.UtcNow.AddMinutes(-10));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);

        var response = await client.GetAsync("/api/secure");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredRsaToken_Returns401Unauthorized()
    {
        using var server = await CreateHostAsync();
        var client = server.GetTestClient();
        var expiredToken = GenerateRsaToken(expires: DateTime.UtcNow.AddMinutes(-10));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);

        var response = await client.GetAsync("/api/secure");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InvalidSignatureSymmetricToken_Returns401Unauthorized()
    {
        using var server = await CreateHostAsync();
        var client = server.GetTestClient();
        var invalidToken = GenerateSymmetricToken(secret: "wrong-symmetric-key-must-be-min-32-chars-long-12345678");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", invalidToken);

        var response = await client.GetAsync("/api/secure");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InvalidSignatureRsaToken_Returns401Unauthorized()
    {
        using var server = await CreateHostAsync();
        var client = server.GetTestClient();
        using var foreignRsa = RSA.Create(2048);
        var invalidToken = GenerateRsaToken(rsa: foreignRsa);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", invalidToken);

        var response = await client.GetAsync("/api/secure");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ValidRsaToken_SucceedsWith200AndPreservesClaims()
    {
        using var server = await CreateHostAsync(includeRsa: true, includeSymmetric: true);
        var client = server.GetTestClient();
        var token = GenerateRsaToken(role: "DRIVER");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/secure");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"role\":\"DRIVER\"", body);
        Assert.Contains("\"isDriver\":true", body);
    }

    [Fact]
    public async Task ValidSymmetricToken_DualKeyConfig_SucceedsWith200()
    {
        using var server = await CreateHostAsync(includeRsa: true, includeSymmetric: true);
        var client = server.GetTestClient();
        var token = GenerateSymmetricToken(role: "DRIVER");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/secure");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"role\":\"DRIVER\"", body);
        Assert.Contains("\"isDriver\":true", body);
    }

    [Fact]
    public async Task RoleBasedEndpoint_DriverRole_Authorized()
    {
        using var server = await CreateHostAsync();
        var client = server.GetTestClient();
        var token = GenerateRsaToken(role: "DRIVER");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/secure/driver-only");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RoleBasedEndpoint_WrongRole_Returns403Forbidden()
    {
        using var server = await CreateHostAsync();
        var client = server.GetTestClient();
        var token = GenerateRsaToken(role: "ADMIN");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/secure/driver-only");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task InvalidIssuer_Returns401Unauthorized()
    {
        using var server = await CreateHostAsync();
        var client = server.GetTestClient();
        var token = GenerateSymmetricToken(issuer: "Wrong.Issuer");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/secure");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FutureNotBefore_Returns401Unauthorized()
    {
        using var server = await CreateHostAsync();
        var client = server.GetTestClient();
        
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_symmetricSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var tokenHandler = new JwtSecurityTokenHandler();
        tokenHandler.InboundClaimTypeMap.Clear();

        var token = tokenHandler.CreateJwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            subject: new ClaimsIdentity(new[]
            {
                new Claim("sub", Guid.NewGuid().ToString()),
                new Claim("role", "DRIVER")
            }),
            notBefore: DateTime.UtcNow.AddMinutes(15),
            expires: DateTime.UtcNow.AddMinutes(45),
            signingCredentials: creds
        );
        var rawToken = tokenHandler.WriteToken(token);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", rawToken);

        var response = await client.GetAsync("/api/secure");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PrivateKeyPemInConfig_ValidatesSuccessfully()
    {
        // When configuration supplies PrivateKeyPem instead of PublicKeyPem
        var builder = new HostBuilder().ConfigureWebHost(web => web.UseTestServer()
            .UseEnvironment("Production")
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    var rsa = RSA.Create();
                    rsa.ImportFromPem(_rsaPrivateKeyPem);
                    var key = new RsaSecurityKey(rsa);

                    options.MapInboundClaims = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = _issuer,
                        ValidateAudience = true,
                        ValidAudience = _audience,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromMinutes(1),
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKeys = new[] { key },
                        IssuerSigningKey = key,
                        RoleClaimType = "role",
                        NameClaimType = "sub"
                    };
                });
                services.AddAuthorization();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/api/secure-pk", [Authorize] () => Results.Ok(new { status = "pk-ok" }));
                });
            }));

        using var server = await builder.StartAsync();
        var client = server.GetTestClient();
        var token = GenerateRsaToken(role: "DRIVER");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/secure-pk");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void CurrentActorAdapter_ExtractsClaimsFromJwtPrincipalCorrectly()
    {
        var expectedUserId = Guid.NewGuid();
        var expectedTenantId = Guid.NewGuid();
        var expectedSiteId = Guid.NewGuid();

        var claims = new[]
        {
            new Claim("sub", expectedUserId.ToString()),
            new Claim("role", "DRIVER"),
            new Claim("tenant_id", expectedTenantId.ToString()),
            new Claim("site_id", expectedSiteId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "Bearer", "sub", "role");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };

        var actor = new SmartParking.PaymentService.Infrastructure.Adapters.CurrentActorAdapter(accessor);

        Assert.True(actor.IsAuthenticated);
        Assert.Equal(expectedUserId, actor.UserId);
        Assert.Equal("DRIVER", actor.Role);
        Assert.Equal(expectedTenantId, actor.TenantId);
        Assert.Equal(expectedSiteId, actor.SiteId);
        Assert.False(actor.IsInternalService);
    }
}
