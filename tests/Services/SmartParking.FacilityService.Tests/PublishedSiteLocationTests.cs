using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SmartParking.FacilityService.API;
using SmartParking.FacilityService.API.Controllers;
using SmartParking.FacilityService.Infrastructure;
using SmartParking.FacilityService.Persistence;

namespace SmartParking.FacilityService.Tests;

public sealed class PublishedSiteLocationTests
{
    private const string Schema = "scripts/database/migrations/20261106_facility_discovery_publication.sql";

    [StructurePostgresFact]
    public async Task PublishedActiveSiteIsReturnedAndWithdrawnOrUnusableSiteIsAbsent()
    {
        await using var fixture = await OccupancyProjectionFixture.CreateAsync(Schema);
        var reader = new PublishedSiteLocationReader(fixture.DataSource);
        Assert.Null(await reader.ReadAsync(fixture.SiteId, default));
        await fixture.ExecuteAsync($"""
            UPDATE parking_sites SET latitude=10.75, longitude=106.70 WHERE id='{fixture.SiteId}';
            INSERT INTO facility_discovery_publications(site_id,tenant_id,configuration_version,lot_type,categories)
            VALUES ('{fixture.SiteId}','{fixture.TenantId}','{Guid.NewGuid()}','outdoor',ARRAY['CAR']);
            """);
        var location = await reader.ReadAsync(fixture.SiteId, default);
        Assert.Equal((fixture.SiteId, "Occupancy Test", 10.75m, 106.70m),
            (location!.SiteId, location.SiteName, location.Latitude, location.Longitude));
        await fixture.ExecuteAsync($"UPDATE parking_sites SET latitude=NULL WHERE id='{fixture.SiteId}'");
        Assert.Null(await reader.ReadAsync(fixture.SiteId, default));
        await fixture.ExecuteAsync($"""
            INSERT INTO facility_discovery_publications(site_id,tenant_id,configuration_version,lot_type,categories)
            VALUES ('{fixture.SiteId}','{fixture.TenantId}','{Guid.NewGuid()}','outdoor',ARRAY['CAR']);
            """);
        Assert.Null(await reader.ReadAsync(fixture.SiteId, default));
        await fixture.ExecuteAsync($"DELETE FROM facility_discovery_publications WHERE site_id='{fixture.SiteId}'");
        Assert.Null(await reader.ReadAsync(fixture.SiteId, default));
    }

    [StructurePostgresFact]
    public async Task PrivateRouteAcceptsOnlyOperationsSiteLocationPermission()
    {
        await using var fixture = await OccupancyProjectionFixture.CreateAsync(Schema);
        using var rsa = RSA.Create(2048);
        await using var host = FacilityHost.Build([], builder =>
        {
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddInMemoryCollection();
            builder.Configuration["Services:Key"] = "historical-location-synthetic-service-key";
            builder.Configuration["ReservationIntegration:Enabled"] = "true";
            builder.Configuration["ReservationIntegration:OperationsPublicKeyPem"] = rsa.ExportRSAPublicKeyPem();
            builder.WebHost.UseTestServer();
            builder.Services.AddControllers().AddApplicationPart(typeof(FacilityHost).Assembly);
            builder.Host.ConfigureContainer<IServiceCollection>((_, services) =>
            {
                services.AddSingleton(fixture.DataSource);
                var worker = services.Single(s => s.ImplementationType == typeof(StructureSynchronizationWorker));
                services.Remove(worker);
            });
        });
        await host.StartAsync();
        using var client = host.GetTestClient();
        var route = $"/internal/discovery/v1/sites/{fixture.SiteId}/location";
        using var anonymous = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        client.DefaultRequestHeaders.Add("X-Service-Key", "historical-location-synthetic-service-key");
        using var legacy = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.Unauthorized, legacy.StatusCode);
        client.DefaultRequestHeaders.Remove("X-Service-Key");
        void Authorize(string permission)
        {
            const string issuer = "SmartParking.OperationService";
            var jwt = new JwtSecurityToken(issuer, "SmartParking.FacilityService",
                [new Claim("sub", issuer), new Claim("sender", issuer), new Claim("permission", permission)],
                expires: DateTime.UtcNow.AddMinutes(1),
                signingCredentials: new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256));
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(jwt));
        }
        Authorize(ReservationServiceAuthentication.DiscoveryCandidates);
        using var wrong = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.Forbidden, wrong.StatusCode);
        Authorize(ReservationServiceAuthentication.DiscoverySiteLocation);
        using var accepted = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.True(accepted.Headers.CacheControl?.NoStore);
        using var candidate = await client.PostAsync(DiscoverySlotCandidatesController.RoutePath, null);
        Assert.Equal(HttpStatusCode.Forbidden, candidate.StatusCode);
    }
}
