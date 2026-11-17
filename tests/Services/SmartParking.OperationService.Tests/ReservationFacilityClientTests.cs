using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Infrastructure.Reservations;

namespace SmartParking.OperationService.Tests;

public sealed class ReservationFacilityClientTests
{
    [Fact]
    public async Task ConsecutiveRequests_SignValidTokensAfterEachRequestRsaIsDisposed()
    {
        using var rsa = RSA.Create(2048);
        var site = Guid.NewGuid();
        var slot = Guid.NewGuid();
        var facts = new FacilityBookingFacts(Guid.NewGuid(), site, slot, "fixture-v1", true, true);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ReservationIntegration:Enabled"] = "true",
            ["ReservationIntegration:AllowHttp"] = "true",
            ["ReservationIntegration:OperationsPrivateKeyPem"] = rsa.ExportPkcs8PrivateKeyPem()
        }).Build();
        var handler = new SignedRequestHandler(rsa.ExportParameters(false), facts);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://facility.test/") };
        var client = new ReservationFacilityClient(http, configuration);
        for (var request = 0; request < 4; request++)
            Assert.Equal(facts, await client.ReadAsync(site, slot, default));
        Assert.Equal(4, handler.Calls);
    }

    private sealed class SignedRequestHandler(RSAParameters publicKey, FacilityBookingFacts facts) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var principal = new JwtSecurityTokenHandler { MapInboundClaims = false }.ValidateToken(
                request.Headers.Authorization!.Parameter, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true, IssuerSigningKey = new RsaSecurityKey(publicKey),
                    ValidateIssuer = true, ValidIssuer = "SmartParking.OperationService",
                    ValidateAudience = true, ValidAudience = "SmartParking.FacilityService",
                    ValidateLifetime = true, ValidAlgorithms = [SecurityAlgorithms.RsaSha256]
                }, out _);
            Assert.Equal("reservation:facility-dependency", principal.FindFirst("permission")?.Value);
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(facts) });
        }
    }
}
