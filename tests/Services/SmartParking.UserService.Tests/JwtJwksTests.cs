using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using UserService.API;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.JwT;
using UserService.Application.DTOs;
using UserService.Infrastructure.Services;

namespace SmartParking.UserService.Tests;

public sealed class JwtJwksTests
{
    [Fact]
    public async Task HttpLoginAndAnonymousJwksUseTheSameConfiguredKeyAcrossHosts()
    {
        using var rsa = RSA.Create(3072);
        var options = Options(rsa.ExportPkcs8PrivateKeyPem());
        var fixture = new AuthTestFixture();
        await using var firstHost = await StartHostAsync(options, fixture);
        using var firstClient = firstHost.GetTestClient();
        using var login = await firstClient.PostAsJsonAsync("/api/auth/login", new { email = fixture.User.Email, password = "Password@123" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var loginBody = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var session = loginBody.RootElement.GetProperty("data");
        var token = session.GetProperty("accessToken").GetString()!;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal("RS256", jwt.Header.Alg);
        Assert.Equal(options.KeyId, jwt.Header.Kid);
        Assert.Equal(options.Issuer, jwt.Issuer);
        Assert.Equal(options.Audience, Assert.Single(jwt.Audiences));
        Assert.Equal(fixture.User.Id!.Value.ToString(), jwt.Subject);
        Assert.Equal(fixture.User.Accounts.Single().Id!.Value.ToString(), jwt.Claims.Single(c => c.Type == "account_id").Value);
        Assert.Equal("driver", jwt.Claims.Single(c => c.Type == "role").Value);
        var stored = Assert.Single(fixture.Sessions.Snapshot);
        Assert.Equal(stored.AccessTokenId.ToString("N"), jwt.Id);
        Assert.NotEqual(stored.Id, Guid.Parse(jwt.Id));
        Assert.Equal(900, session.GetProperty("expiresIn").GetInt32());
        Assert.Equal(900, (jwt.ValidTo - jwt.ValidFrom).TotalSeconds);

        using var response = await firstClient.GetAsync(options.JwksPath);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/jwk-set+json", response.Content.Headers.ContentType!.MediaType);
        Assert.True(response.Headers.CacheControl!.Public);
        Assert.Equal(TimeSpan.FromSeconds(300), response.Headers.CacheControl.MaxAge);
        var document = await response.Content.ReadAsStringAsync();
        using var jwks = JsonDocument.Parse(document);
        Assert.Equal(new[] { "keys" }, jwks.RootElement.EnumerateObject().Select(p => p.Name));
        var publicKey = Assert.Single(jwks.RootElement.GetProperty("keys").EnumerateArray());
        Assert.Equal(new[] { "alg", "e", "kid", "kty", "n", "use" }, publicKey.EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal("RSA", publicKey.GetProperty("kty").GetString());
        Assert.Equal("sig", publicKey.GetProperty("use").GetString());
        Assert.Equal("RS256", publicKey.GetProperty("alg").GetString());
        Assert.Equal(jwt.Header.Kid, publicKey.GetProperty("kid").GetString());
        var parameters = rsa.ExportParameters(false);
        Assert.Equal(Base64UrlEncoder.Encode(parameters.Modulus!), publicKey.GetProperty("n").GetString());
        Assert.Equal(Base64UrlEncoder.Encode(parameters.Exponent!), publicKey.GetProperty("e").GetString());
        Assert.DoesNotContain(options.PrivateKeyPemBase64, document);
        Assert.DoesNotContain("PRIVATE KEY", document);
        var validation = PublicValidation(publicKey.GetRawText(), options, fixture.Clock);
        Assert.Equal(jwt.Subject, Validate(token, validation).FindFirst("sub")!.Value);

        await firstHost.StopAsync();
        await using var restartedHost = await StartHostAsync(options, fixture);
        using var restartedClient = restartedHost.GetTestClient();
        Assert.Equal(document, await restartedClient.GetStringAsync(options.JwksPath));
        using var scope = restartedHost.Services.CreateScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<IAccessTokenService>().ValidateAccessTokenAsync(token));
        restartedClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        using var originalProfile = await restartedClient.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, originalProfile.StatusCode);
        // Real controller/handler/session/token composition; storage is the existing in-memory test adapter.
        using var refresh = await restartedClient.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = session.GetProperty("refreshToken").GetString() });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        using var refreshedBody = JsonDocument.Parse(await refresh.Content.ReadAsStringAsync());
        var refreshedToken = refreshedBody.RootElement.GetProperty("data").GetProperty("accessToken").GetString()!;
        Assert.Equal(options.KeyId, new JwtSecurityTokenHandler().ReadJwtToken(refreshedToken).Header.Kid);
        Validate(refreshedToken, validation);
        restartedClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", refreshedToken);
        using var refreshedProfile = await restartedClient.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, refreshedProfile.StatusCode);
        Assert.False(await scope.ServiceProvider.GetRequiredService<IAccessTokenService>().ValidateAccessTokenAsync(token));
    }

    [Theory]
    [InlineData("key")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("algorithm")]
    [InlineData("expired")]
    public async Task PublicJwkRejectsUntrustedOrInvalidTokens(string invalid)
    {
        using var rsa = RSA.Create(2048);
        using var attacker = RSA.Create(2048);
        var options = Options(rsa.ExportPkcs8PrivateKeyPem());
        var fixture = new AuthTestFixture();
        await using var app = await StartHostAsync(options, fixture);
        using var client = app.GetTestClient();
        using var jwks = JsonDocument.Parse(await client.GetStringAsync(options.JwksPath));
        var key = jwks.RootElement.GetProperty("keys")[0];
        var now = fixture.Clock.GetUtcNow().UtcDateTime;
        var forged = new JwtSecurityToken(invalid == "issuer" ? "untrusted" : options.Issuer,
            invalid == "audience" ? "untrusted" : options.Audience,
            [new Claim("sub", fixture.User.Id!.Value.ToString()), new Claim("account_id", fixture.User.Accounts.Single().Id!.Value.ToString()),
             new Claim("jti", Guid.NewGuid().ToString("N")), new Claim("role", "driver")],
            now.AddMinutes(-10), invalid == "expired" ? now.AddMinutes(-1) : now.AddMinutes(5),
            new SigningCredentials(new RsaSecurityKey(invalid == "key" ? attacker : rsa) { KeyId = options.KeyId },
                invalid == "algorithm" ? SecurityAlgorithms.RsaSha384 : SecurityAlgorithms.RsaSha256));
        // Token-controlled URLs do not alter the configured trust key.
        forged.Header["jku"] = "https://untrusted.invalid/jwks.json";
        forged.Header["x5u"] = "https://untrusted.invalid/cert";
        Assert.ThrowsAny<SecurityTokenException>(() => Validate(new JwtSecurityTokenHandler().WriteToken(forged),
            PublicValidation(key.GetRawText(), options, fixture.Clock)));
    }

    [Fact]
    public void LegacyPemHasStableThumbprintIdAndMatchingDualInputsAreAccepted()
    {
        using var rsa = RSA.Create(2048);
        var options = Options(rsa.ExportPkcs8PrivateKeyPem());
        options.KeyId = string.Empty;
        options.PrivateKeyPem = rsa.ExportRSAPrivateKeyPem();
        using var dual = new JwtKeyProvider(options);
        options.PrivateKeyPemBase64 = string.Empty;
        using var legacy = new JwtKeyProvider(options);
        Assert.Equal(Base64UrlEncoder.Encode(legacy.ValidationKey.ComputeJwkThumbprint()), legacy.KeyId);
        Assert.Equal(dual.KeyId, legacy.KeyId);
        Assert.Equal(dual.PublicModulus, legacy.PublicModulus);
        Assert.False(legacy.IsEphemeral);
    }

    [Fact]
    public void ConflictingOrMalformedDualInputsFailWithoutEchoingSecrets()
    {
        using var first = RSA.Create(2048);
        using var second = RSA.Create(2048);
        var options = Options(first.ExportPkcs8PrivateKeyPem());
        options.PrivateKeyPem = second.ExportRSAPrivateKeyPem();
        var conflict = Assert.Throws<InvalidOperationException>(() => new JwtKeyProvider(options, true));
        Assert.Contains("same RSA private key", conflict.Message);
        Assert.Null(conflict.InnerException);
        options.PrivateKeyPem = "invalid-private-input-must-never-appear";
        var malformed = Assert.Throws<InvalidOperationException>(() => new JwtKeyProvider(options, true));
        Assert.DoesNotContain(options.PrivateKeyPem, malformed.ToString());
        Assert.Null(malformed.InnerException);
    }

    [Theory]
    [InlineData("base64")]
    [InlineData("placeholder")]
    [InlineData("pem")]
    [InlineData("public-only")]
    [InlineData("undersized")]
    [InlineData("encrypted")]
    public void InvalidConfiguredPrivateKeyNeverFallsBackToEphemeral(string kind)
    {
        using var rsa = RSA.Create(kind == "undersized" ? 1024 : 2048);
        var options = Options(rsa.ExportPkcs8PrivateKeyPem());
        options.KeyId = string.Empty;
        if (kind == "base64") options.PrivateKeyPemBase64 = "not valid Base64 !";
        if (kind == "placeholder") options.PrivateKeyPemBase64 = "replace-with-base64-rsa-private-key";
        if (kind == "pem") options.PrivateKeyPemBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("invalid-pem-secret"));
        if (kind == "public-only") options.PrivateKeyPemBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(rsa.ExportSubjectPublicKeyInfoPem()));
        if (kind == "encrypted") options.PrivateKeyPemBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(rsa.ExportEncryptedPkcs8PrivateKeyPem("test", new(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1000))));
        var error = Assert.Throws<InvalidOperationException>(() => new JwtKeyProvider(options, true));
        Assert.Null(error.InnerException);
        Assert.DoesNotContain(options.PrivateKeyPemBase64, error.ToString());
    }

    [Fact]
    public void OnlyUnconfiguredStandaloneDevelopmentMayUseEphemeralKeys()
    {
        var options = new JwtOptions { Issuer = "issuer", Audience = "audience" };
        Assert.Throws<InvalidOperationException>(() => new JwtKeyProvider(options));
        using var first = new JwtKeyProvider(options, true);
        using var restarted = new JwtKeyProvider(options, true);
        Assert.True(first.IsEphemeral);
        Assert.NotEqual(first.PublicModulus, restarted.PublicModulus);
        Assert.NotEqual(first.KeyId, restarted.KeyId);
        options.KeyId = "shared-key-01";
        Assert.Throws<InvalidOperationException>(() => new JwtKeyProvider(options, true));
    }

    [Theory]
    [InlineData("issuer", "")]
    [InlineData("audience", " ")]
    [InlineData("kid", "unsafe kid")]
    [InlineData("path", "https://untrusted.invalid/jwks")]
    [InlineData("path", "/jwks/{key}")]
    [InlineData("path", "/jwks?query")]
    [InlineData("path", "/jwks#fragment")]
    [InlineData("path", "/../jwks")]
    public void InvalidTrustOrPublicationConfigurationFails(string field, string value)
    {
        var options = new JwtOptions { Issuer = "issuer", Audience = "audience" };
        if (field == "issuer") options.Issuer = value;
        if (field == "audience") options.Audience = value;
        if (field == "kid") options.KeyId = value;
        if (field == "path") options.JwksPath = value;
        Assert.Throws<InvalidOperationException>(() => new JwtKeyProvider(options, true));
    }

    [Theory]
    [InlineData("Development", "shared-key", "")]
    [InlineData("Testing", "", "")]
    [InlineData("Production", "", "")]
    [InlineData("Development", "", "replace-with-base64-rsa-private-key")]
    public async Task HostFailsBeforeStartupForMissingSharedOrInvalidPrivateKey(string environment, string kid, string key)
    {
        var options = new JwtOptions { Issuer = "issuer", Audience = "audience", KeyId = kid, PrivateKeyPemBase64 = key };
        await Assert.ThrowsAsync<InvalidOperationException>(() => BuildHostAsync(options, new AuthTestFixture(), environment));
    }

    [Fact]
    public async Task ConfiguredLiteralPublicationPathIsMappedAnonymously()
    {
        using var rsa = RSA.Create(2048);
        var options = Options(rsa.ExportPkcs8PrivateKeyPem());
        options.JwksPath = "/.well-known/smartpark-keys.json";
        await using var app = await StartHostAsync(options, new AuthTestFixture());
        using var client = app.GetTestClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(options.JwksPath)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/.well-known/jwks.json")).StatusCode);
    }

    private static JwtOptions Options(string pem) => new()
    {
        Issuer = "jwks-test-issuer", Audience = "jwks-test-audience", KeyId = "test-signing-key-01",
        PrivateKeyPemBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(pem))
    };

    private static TokenValidationParameters PublicValidation(string jwk, JwtOptions options, TimeProvider clock) => new()
    {
        ValidateIssuer = true, ValidIssuer = options.Issuer, ValidateAudience = true, ValidAudience = options.Audience,
        ValidateIssuerSigningKey = true, IssuerSigningKey = new JsonWebKey(jwk), RequireSignedTokens = true,
        ValidAlgorithms = [SecurityAlgorithms.RsaSha256], ValidateLifetime = true, RequireExpirationTime = true, ClockSkew = TimeSpan.Zero,
        LifetimeValidator = (notBefore, expires, _, _) => expires > clock.GetUtcNow().UtcDateTime
            && (!notBefore.HasValue || notBefore <= clock.GetUtcNow().UtcDateTime)
    };

    private static ClaimsPrincipal Validate(string token, TokenValidationParameters validation) =>
        new JwtSecurityTokenHandler { MapInboundClaims = false }.ValidateToken(token, validation, out _);

    private static async Task<WebApplication> StartHostAsync(JwtOptions options, AuthTestFixture fixture)
    {
        var app = await BuildHostAsync(options, fixture, "Testing");
        await app.StartAsync();
        return app;
    }

    private static Task<WebApplication> BuildHostAsync(JwtOptions options, AuthTestFixture fixture, string environment) =>
        UserHost.BuildAsync(["--environment", environment], builder =>
        {
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:User"] = "Host=127.0.0.1;Database=unused_jwks_tests;Username=unused",
                ["Services:Parking"] = "http://parking.test/", ["Services:Key"] = new string('x', 40),
                ["Messaging:Enabled"] = "false", ["Jwt:Issuer"] = options.Issuer, ["Jwt:Audience"] = options.Audience,
                ["Jwt:PrivateKeyPem"] = options.PrivateKeyPem, ["Jwt:PrivateKeyPemBase64"] = options.PrivateKeyPemBase64,
                ["Jwt:KeyId"] = options.KeyId, ["Jwt:JwksPath"] = options.JwksPath,
                ["Workflow:KeyDirectory"] = environment == "Production" ? Path.GetTempPath() : null
            });
        }, builder =>
        {
            builder.Services.RemoveAll<IHostedService>();
            builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
            builder.Services.AddSingleton<TimeProvider>(fixture.Clock);
            builder.Services.AddSingleton<IUnitOfWork>(fixture);
            builder.Services.AddSingleton<IAuthSessionStore>(fixture.Sessions);
            builder.Services.AddSingleton<ICurrentAccessStore>(fixture);
            builder.Services.AddSingleton<IMfaPolicy, NoMfa>();
        });

    private sealed class NoMfa : IMfaPolicy
    {
        public Task<bool> MfaEnabledAsync(Guid userId, CancellationToken ct) => Task.FromResult(false);
        public Task<MfaLoginChallengeDto> CreateMfaLoginChallengeAsync(Guid userId, CancellationToken ct) => throw new InvalidOperationException();
    }
}
