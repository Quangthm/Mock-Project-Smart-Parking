using System.Net;
using System.Text;
using System.Text.Json;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Infrastructure.Clients;
using SmartParking.OperationService.Tests.TestDoubles;

namespace SmartParking.OperationService.Tests;

public sealed class RealtimeRoleSubscriptionAuthorizerTests
{
    [Fact]
    public async Task AuthorizeOwnerSitesAsync_UsesSingleBatchedFacilityCall_AndAuthorizesOwnedSites()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ownedSites = Enumerable.Range(0, 15).Select(_ => Guid.NewGuid()).ToArray();
        var userCalls = 0;
        var facilityCalls = 0;

        var authorizer = CreateAuthorizer(
            userHandler: _ =>
            {
                userCalls++;
                return JsonResponse(HttpStatusCode.OK, new { userId, tenantIds = new[] { tenantId } });
            },
            facilityHandler: req =>
            {
                if (req.RequestUri!.AbsolutePath == "/api/parking-lots")
                {
                    facilityCalls++;
                    return JsonResponse(HttpStatusCode.OK, new
                    {
                        success = true,
                        data = ownedSites.Select(id => new { id, tenantId }).ToArray()
                    });
                }
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

        await authorizer.AuthorizeOwnerSitesAsync(ownedSites, "test-token", CancellationToken.None);

        Assert.Equal(1, userCalls);
        Assert.Equal(1, facilityCalls);
    }

    [Fact]
    public async Task AuthorizeOwnerSitesAsync_RejectsMissingOrDeletedSite_AndSiteOutsideAuthorizedTenants()
    {
        var ownerTenant = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ownedSite = Guid.NewGuid();
        var foreignTenantSite = Guid.NewGuid();
        var missingOrDeletedSite = Guid.NewGuid();

        var authorizer = CreateAuthorizer(
            userHandler: _ => JsonResponse(HttpStatusCode.OK, new { userId, tenantIds = new[] { ownerTenant } }),
            facilityHandler: req =>
            {
                if (req.RequestUri!.AbsolutePath == "/api/parking-lots")
                {
                    return JsonResponse(HttpStatusCode.OK, new
                    {
                        success = true,
                        data = new[]
                        {
                            new { id = ownedSite, tenantId = ownerTenant },
                            new { id = foreignTenantSite, tenantId = otherTenant }
                        }
                    });
                }
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

        var foreignEx = await Assert.ThrowsAsync<ReservationGateException>(() =>
            authorizer.AuthorizeOwnerSitesAsync([ownedSite, foreignTenantSite], "test-token", CancellationToken.None));
        Assert.Equal(403, foreignEx.StatusCode);
        Assert.Equal("OWNER_SITE_UNAUTHORIZED", foreignEx.Code);

        var deletedEx = await Assert.ThrowsAsync<ReservationGateException>(() =>
            authorizer.AuthorizeOwnerSitesAsync([missingOrDeletedSite], "test-token", CancellationToken.None));
        Assert.Equal(403, deletedEx.StatusCode);
        Assert.Equal("OWNER_SITE_UNAUTHORIZED", deletedEx.Code);
    }

    [Fact]
    public async Task AuthorizeOwnerSitesAsync_FailsClosedWhenFacilityOrUserAuthorityIsUnavailable()
    {
        var ownerTenant = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var facilityUnavailableAuthorizer = CreateAuthorizer(
            userHandler: _ => JsonResponse(HttpStatusCode.OK, new { userId, tenantIds = new[] { ownerTenant } }),
            facilityHandler: _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var facilityEx = await Assert.ThrowsAsync<ReservationGateException>(() =>
            facilityUnavailableAuthorizer.AuthorizeOwnerSitesAsync([siteId], "test-token", CancellationToken.None));
        Assert.Equal(503, facilityEx.StatusCode);
        Assert.Equal("FACILITY_UNAVAILABLE", facilityEx.Code);

        var userUnavailableAuthorizer = CreateAuthorizer(
            userHandler: _ => throw new HttpRequestException("UserService unreachable"),
            facilityHandler: _ => new HttpResponseMessage(HttpStatusCode.OK));

        var userEx = await Assert.ThrowsAsync<ReservationGateException>(() =>
            userUnavailableAuthorizer.AuthorizeOwnerSitesAsync([siteId], "test-token", CancellationToken.None));
        Assert.Equal(503, userEx.StatusCode);
        Assert.Equal("IDENTITY_UNAVAILABLE", userEx.Code);
    }

    [Fact]
    public async Task AuthorizeDriverSitesAsync_EnforcesDiscoverabilityContract()
    {
        var activeSite = Guid.NewGuid();
        var hiddenSite = Guid.NewGuid();

        var authorizer = CreateAuthorizer(
            userHandler: _ => new HttpResponseMessage(HttpStatusCode.OK),
            facilityHandler: req =>
            {
                if (req.RequestUri!.AbsolutePath == $"/api/parking-lots/{activeSite}/operational-layout")
                    return JsonResponse(HttpStatusCode.OK, new { success = true, data = new { siteId = activeSite } });
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

        await authorizer.AuthorizeDriverSitesAsync([activeSite], "driver-token", CancellationToken.None);

        var deniedEx = await Assert.ThrowsAsync<ReservationGateException>(() =>
            authorizer.AuthorizeDriverSitesAsync([activeSite, hiddenSite], "driver-token", CancellationToken.None));
        Assert.Equal(403, deniedEx.StatusCode);
        Assert.Equal("SITE_NOT_DISCOVERABLE", deniedEx.Code);
    }

    private static IRealtimeRoleSubscriptionAuthorizer CreateAuthorizer(
        Func<HttpRequestMessage, HttpResponseMessage> userHandler,
        Func<HttpRequestMessage, HttpResponseMessage> facilityHandler)
    {
        var factory = new StubHttpClientFactory(
            new HttpClient(new StubHttpMessageHandler(userHandler)) { BaseAddress = new Uri("http://user-service/") },
            new HttpClient(new StubHttpMessageHandler(facilityHandler)) { BaseAddress = new Uri("http://facility-service/") });
        return new RealtimeRoleSubscriptionAuthorizer(factory);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, object payload) =>
        new(statusCode)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
}
