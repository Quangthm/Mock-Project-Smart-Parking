using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SmartParking.PaymentService.API.Authentication;

namespace SmartParking.PaymentService.Tests.Authentication;

public sealed class OperationsServiceAuthenticationTests
{
    [Fact]
    public void EnabledConfiguration_WithoutPublicKey_FailsClosed()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["ReservationIntegration:Enabled"] = "true"
        });

        Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddOperationsServiceAuthentication(configuration));
    }

    [Fact]
    public void ConfiguredScheme_ValidatesOperationsTokenAndPolicy()
    {
        using var rsa = RSA.Create(2048);
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["ReservationIntegration:Enabled"] = "true",
            ["ReservationIntegration:OperationsPublicKeyPem"] = rsa.ExportSubjectPublicKeyInfoPem()
        });
        var services = new ServiceCollection();
        services.AddOperationsServiceAuthentication(configuration);
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(OperationsServiceAuthentication.Scheme);
        var token = Token(rsa.ExportRSAPrivateKeyPem(), "SmartParking.PaymentService");
        var principal = new JwtSecurityTokenHandler()
            .ValidateToken(token, options.TokenValidationParameters, out _);

        Assert.True(principal.Identity?.IsAuthenticated);
        Assert.Contains(principal.Claims,
            claim => claim.Type == "permission" && claim.Value == OperationsServiceAuthentication.Permission);

        var policy = provider.GetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider>()
            .GetPolicyAsync(OperationsServiceAuthentication.Policy).GetAwaiter().GetResult();
        Assert.NotNull(policy);
        Assert.Contains(OperationsServiceAuthentication.Scheme, policy.AuthenticationSchemes);
    }

    [Fact]
    public void ConfiguredScheme_RejectsTokenForAnotherAudience()
    {
        using var rsa = RSA.Create(2048);
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["ReservationIntegration:Enabled"] = "true",
            ["ReservationIntegration:OperationsPublicKeyPem"] = rsa.ExportSubjectPublicKeyInfoPem()
        });
        var services = new ServiceCollection();
        services.AddOperationsServiceAuthentication(configuration);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(OperationsServiceAuthentication.Scheme);
        var token = Token(rsa.ExportRSAPrivateKeyPem(), "SmartParking.FacilityService");

        Assert.ThrowsAny<SecurityTokenInvalidAudienceException>(() =>
            new JwtSecurityTokenHandler().ValidateToken(token, options.TokenValidationParameters, out _));
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static string Token(string privateKeyPem, string audience)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken("SmartParking.OperationService", audience,
            [
                new Claim("sub", "SmartParking.OperationService"),
                new Claim("sender", "SmartParking.OperationService"),
                new Claim("permission", OperationsServiceAuthentication.Permission),
                new Claim("role", "INTERNAL_SERVICE"),
                new Claim("scope", "s2s"),
                new Claim("jti", Guid.NewGuid().ToString("N"))
            ],
            now, now.AddMinutes(1),
            new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
