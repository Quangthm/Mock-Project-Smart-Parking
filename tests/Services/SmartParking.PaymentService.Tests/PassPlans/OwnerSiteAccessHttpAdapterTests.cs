using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using SmartParking.PaymentService.Domain.Exceptions;
using SmartParking.PaymentService.Infrastructure.Adapters.Outbound;

namespace SmartParking.PaymentService.Tests.PassPlans;

public sealed class OwnerSiteAccessHttpAdapterTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-4000-8000-000000000001");
    private static readonly Guid TenantId = Guid.Parse("22222222-2222-4000-8000-000000000001");
    private static readonly Guid SiteId = Guid.Parse("33333333-3333-4000-8000-000000000001");

    private static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task ValidateOwnerSiteAsync_ValidUserScopeAndFacilityValidate_ReturnsScope()
    {
        using var userTransport = new PaymentStubHttpHandler(_ =>
            Json(HttpStatusCode.OK, $$"""{"userId":"{{UserId}}","tenantIds":["{{TenantId}}"]}"""));
        using var facilityTransport = new PaymentStubHttpHandler(req =>
        {
            Assert.Equal("internal-key", req.Headers.GetValues("X-Service-Key").Single());
            return Json(HttpStatusCode.OK, $$"""[{"id":"{{SiteId}}","tenantId":"{{TenantId}}"}]""");
        });

        using var userClient = new HttpClient(userTransport) { BaseAddress = new Uri("https://user.test/") };
        using var facilityClient = new HttpClient(facilityTransport) { BaseAddress = new Uri("https://facility.test/") };
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.Authorization = "Bearer valid-owner-jwt";

        var adapter = new OwnerSiteAccessHttpAdapter(
            userClient,
            facilityClient,
            new HttpContextAccessor { HttpContext = ctx },
            "internal-key");

        var scope = await adapter.ValidateOwnerSiteAsync(SiteId, CancellationToken.None);

        Assert.Equal(UserId, scope.UserId);
        Assert.Equal(TenantId, scope.TenantId);
        Assert.Equal(SiteId, scope.SiteId);
    }

    [Fact]
    public async Task ValidateOwnerSiteAsync_FallbackLotsListingAndForeignOrMissingSiteChecksWork()
    {
        using var userTransport = new PaymentStubHttpHandler(_ =>
            Json(HttpStatusCode.OK, $$"""{"userId":"{{UserId}}","tenantIds":["{{TenantId}}"]}"""));
        using var facilityTransport = new PaymentStubHttpHandler(req =>
        {
            var path = req.RequestUri?.PathAndQuery ?? string.Empty;
            if (path.StartsWith("/api/parking-lots?tenantId=", StringComparison.OrdinalIgnoreCase))
            {
                return Json(HttpStatusCode.OK, $$"""{"success":true,"data":[{"id":"{{SiteId}}","tenantId":"{{TenantId}}"}]}""");
            }
            return Json(HttpStatusCode.NotFound, "{}");
        });

        using var userClient = new HttpClient(userTransport) { BaseAddress = new Uri("https://user.test/") };
        using var facilityClient = new HttpClient(facilityTransport) { BaseAddress = new Uri("https://facility.test/") };
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.Authorization = "Bearer valid-owner-jwt";

        var adapter = new OwnerSiteAccessHttpAdapter(
            userClient,
            facilityClient,
            new HttpContextAccessor { HttpContext = ctx });

        var scope = await adapter.ValidateOwnerSiteAsync(SiteId, CancellationToken.None);
        Assert.Equal(TenantId, scope.TenantId);

        // Site exists in another tenant -> FORBIDDEN_SITE_SCOPE
        var otherSiteId = Guid.NewGuid();
        using var foreignFacilityTransport = new PaymentStubHttpHandler(req =>
        {
            var path = req.RequestUri?.PathAndQuery ?? string.Empty;
            if (path.StartsWith("/api/parking-lots?tenantId=", StringComparison.OrdinalIgnoreCase))
            {
                return Json(HttpStatusCode.OK, """{"success":true,"data":[]}""");
            }
            return Json(HttpStatusCode.OK, """{"success":true,"data":{"siteId":"other"}}""");
        });
        using var foreignClient = new HttpClient(foreignFacilityTransport) { BaseAddress = new Uri("https://facility.test/") };
        var foreignAdapter = new OwnerSiteAccessHttpAdapter(
            userClient,
            foreignClient,
            new HttpContextAccessor { HttpContext = ctx });
        var forbidden = await Assert.ThrowsAsync<PaymentDomainException>(() =>
            foreignAdapter.ValidateOwnerSiteAsync(otherSiteId, CancellationToken.None));
        Assert.Equal("FORBIDDEN_SITE_SCOPE", forbidden.Code);

        // Site not found anywhere -> SITE_NOT_FOUND
        using var missingTransport = new PaymentStubHttpHandler(req =>
        {
            var path = req.RequestUri?.PathAndQuery ?? string.Empty;
            if (path.StartsWith("/api/parking-lots?tenantId=", StringComparison.OrdinalIgnoreCase))
            {
                return Json(HttpStatusCode.OK, """{"success":true,"data":[]}""");
            }
            return Json(HttpStatusCode.NotFound, "{}");
        });
        using var missingClient = new HttpClient(missingTransport) { BaseAddress = new Uri("https://facility.test/") };
        var missingAdapter = new OwnerSiteAccessHttpAdapter(
            userClient,
            missingClient,
            new HttpContextAccessor { HttpContext = ctx });
        var notFound = await Assert.ThrowsAsync<PaymentDomainException>(() =>
            missingAdapter.ValidateOwnerSiteAsync(otherSiteId, CancellationToken.None));
        Assert.Equal("SITE_NOT_FOUND", notFound.Code);
    }

    [Fact]
    public async Task ValidateOwnerSiteAsync_InvalidJsonOrAuthFailures_MapProperCodes()
    {
        // Missing bearer -> UNAUTHENTICATED
        using var okTransport = new PaymentStubHttpHandler(_ => Json(HttpStatusCode.OK, "{}"));
        using var okClient = new HttpClient(okTransport) { BaseAddress = new Uri("https://user.test/") };
        var noAuthAdapter = new OwnerSiteAccessHttpAdapter(
            okClient,
            okClient,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() });
        var unauth = await Assert.ThrowsAsync<PaymentDomainException>(() =>
            noAuthAdapter.ValidateOwnerSiteAsync(SiteId, CancellationToken.None));
        Assert.Equal("UNAUTHENTICATED", unauth.Code);

        // Corrupted JSON from UserService -> IDENTITY_UNAVAILABLE
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.Authorization = "Bearer valid-owner-jwt";
        using var badUserTransport = new PaymentStubHttpHandler(_ => Json(HttpStatusCode.OK, "not-json"));
        using var badUserClient = new HttpClient(badUserTransport) { BaseAddress = new Uri("https://user.test/") };
        var badUserAdapter = new OwnerSiteAccessHttpAdapter(
            badUserClient,
            okClient,
            new HttpContextAccessor { HttpContext = ctx });
        var idUnavailable = await Assert.ThrowsAsync<PaymentDomainException>(() =>
            badUserAdapter.ValidateOwnerSiteAsync(SiteId, CancellationToken.None));
        Assert.Equal("IDENTITY_UNAVAILABLE", idUnavailable.Code);

        // Corrupted JSON from FacilityService -> FACILITY_UNAVAILABLE
        using var goodUserTransport = new PaymentStubHttpHandler(_ =>
            Json(HttpStatusCode.OK, $$"""{"userId":"{{UserId}}","tenantIds":["{{TenantId}}"]}"""));
        using var goodUserClient = new HttpClient(goodUserTransport) { BaseAddress = new Uri("https://user.test/") };
        using var badFacilityTransport = new PaymentStubHttpHandler(_ => Json(HttpStatusCode.OK, "not-json"));
        using var badFacilityClient = new HttpClient(badFacilityTransport) { BaseAddress = new Uri("https://facility.test/") };
        var badFacilityAdapter = new OwnerSiteAccessHttpAdapter(
            goodUserClient,
            badFacilityClient,
            new HttpContextAccessor { HttpContext = ctx },
            "internal-key");
        var facUnavailable = await Assert.ThrowsAsync<PaymentDomainException>(() =>
            badFacilityAdapter.ValidateOwnerSiteAsync(SiteId, CancellationToken.None));
        Assert.Equal("FACILITY_UNAVAILABLE", facUnavailable.Code);
    }
}
