using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Application.Policies.DTOs;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Domain.PassPlans;
using SmartParking.OperationService.Infrastructure.PassPlans;
using SmartParking.OperationService.Persistence;
using SmartParking.OperationService.Persistence.Repositories;

namespace SmartParking.OperationService.Tests.PassPlans;

public sealed class PassPlanAdapterTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000010");
    private static readonly Guid TenantId = Guid.Parse("bbbbbbbb-0000-4000-8000-000000000010");
    private static readonly Guid SiteId = Guid.Parse("cccccccc-0000-4000-8000-000000000010");
    private static readonly Guid ZoneId = Guid.Parse("dddddddd-0000-4000-8000-000000000010");

    private static ReservationDbContext CreateInMemoryDb() =>
        new(new DbContextOptionsBuilder<ReservationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task PaymentPassTermsHttpAdapter_ValidEnvelopeWithStringEnums_ParsesAndSendsSignedServiceJwt()
    {
        using var rsa = RSA.Create(2048);
        var privatePem = rsa.ExportPkcs8PrivateKeyPem();
        var termId = Guid.NewGuid();
        var planId = Guid.NewGuid();

        using var transport = new PassPlanStubHttpMessageHandler(_ => JsonResponse(
            HttpStatusCode.OK,
            $$"""
            {
              "success": true,
              "data": [
                {
                  "financialTermId": "{{termId}}",
                  "siteId": "{{SiteId}}",
                  "planId": "{{planId}}",
                  "planRevision": 2,
                  "financialTermRevision": 1,
                  "productType": "MONTHLY",
                  "currency": "VND",
                  "isActive": true,
                  "vehiclePrices": [
                    { "vehicleType": "CAR", "fullPeriodPrice": 1500000 },
                    { "vehicleType": "MOTORCYCLE", "fullPeriodPrice": 180000 }
                  ],
                  "refundPolicy": {
                    "voluntaryBeforeStartPercent": 100,
                    "voluntaryInPeriodPercent": 50,
                    "facilityClosurePercent": 100,
                    "roundingMode": "HALF_UP_THOUSAND"
                  },
                  "effectiveFrom": "2026-11-17T03:11:00Z"
                }
              ]
            }
            """));

        using var http = new HttpClient(transport) { BaseAddress = new Uri("https://payment.test/") };
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ReservationIntegration:Enabled"] = "true",
            ["ReservationIntegration:OperationsPrivateKeyPem"] = privatePem
        }).Build();

        var adapter = new PaymentPassTermsHttpAdapter(http, cfg);
        var terms = await adapter.GetActiveFinancialTermsAsync(SiteId, planId, CancellationToken.None);

        var term = Assert.Single(terms);
        Assert.Equal(termId, term.FinancialTermId);
        Assert.Equal("MONTHLY", term.ProductType);
        Assert.Equal(2, term.VehiclePrices.Count);
        Assert.Equal("HALF_UP_THOUSAND", term.RefundPolicy.RoundingMode);
        Assert.False(transport.HasServiceKeyHeader);
        Assert.NotNull(transport.LastAuthorization);
        Assert.StartsWith("Bearer ", transport.LastAuthorization, StringComparison.Ordinal);

        var token = transport.LastAuthorization!["Bearer ".Length..];
        using var publicRsa = RSA.Create();
        publicRsa.ImportParameters(rsa.ExportParameters(false));
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "SmartParking.OperationService",
            ValidateAudience = true,
            ValidAudience = "SmartParking.PaymentService",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new RsaSecurityKey(publicRsa),
            ValidateLifetime = true
        }, out _);

        Assert.Equal("pass:financial-terms:read", principal.FindFirst("permission")?.Value);
        Assert.Equal("SmartParking.OperationService", principal.FindFirst("sender")?.Value);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, "{broken-json")]
    [InlineData(HttpStatusCode.OK, """{"success":false,"data":[]}""")]
    [InlineData(HttpStatusCode.InternalServerError, """{"success":false,"errorCode":"INTERNAL_SERVER_ERROR"}""")]
    public async Task PaymentPassTermsHttpAdapter_InvalidOrErrorPayloads_FailClosedWith503(HttpStatusCode status, string body)
    {
        using var rsa = RSA.Create(2048);
        using var transport = new PassPlanStubHttpMessageHandler(_ => JsonResponse(status, body));
        using var http = new HttpClient(transport) { BaseAddress = new Uri("https://payment.test/") };
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ReservationIntegration:Enabled"] = "true",
            ["ReservationIntegration:OperationsPrivateKeyPem"] = rsa.ExportPkcs8PrivateKeyPem()
        }).Build();

        var adapter = new PaymentPassTermsHttpAdapter(http, cfg);
        var ex = await Assert.ThrowsAsync<PassPlanException>(() =>
            adapter.GetActiveFinancialTermsAsync(SiteId, null, CancellationToken.None));

        Assert.Equal(503, ex.StatusCode);
    }

    [Fact]
    public async Task PassPlanFacilityScopeAdapter_ValidStructureAndOperationalLayout_SupportsCarAndMotorcycle()
    {
        await using var db = CreateInMemoryDb();
        var repo = new PassPlanRepository(db);
        var identity = System.Reflection.DispatchProxy.Create<IOwnerIdentityClient, DelegateOwnerIdentityProxy>();
        ((DelegateOwnerIdentityProxy)identity).GetOwnerScopeAsyncFunc = (_, _) =>
            Task.FromResult(new OwnerIdentityScope(UserId, [TenantId]));

        using var transport = new PassPlanStubHttpMessageHandler(req =>
        {
            var path = req.RequestUri?.PathAndQuery ?? string.Empty;
            if (path.Equals("/internal/sites/validate", StringComparison.OrdinalIgnoreCase))
            {
                return JsonResponse(HttpStatusCode.OK, $$"""[{"id":"{{SiteId}}","tenantId":"{{TenantId}}"}]""");
            }
            if (path.StartsWith($"/api/parking-lots/{SiteId:D}/structure", StringComparison.OrdinalIgnoreCase))
            {
                return JsonResponse(HttpStatusCode.OK, $$"""
                {
                  "success": true,
                  "data": {
                    "units": [{ "id": "{{ZoneId}}", "parentId": null, "type": "ZONE", "path": "/Z1" }],
                    "slots": [{ "id": "{{Guid.NewGuid()}}", "unitId": "{{ZoneId}}", "vehicleType": "CAR", "vehicleTypes": ["CAR"] }],
                    "capacityViews": [{ "vehicleType": "CAR", "total": 10 }]
                  }
                }
                """);
            }
            if (path.Equals($"/api/parking-lots/{SiteId:D}/operational-layout", StringComparison.OrdinalIgnoreCase))
            {
                return JsonResponse(HttpStatusCode.OK, """
                {
                  "success": true,
                  "data": {
                    "metrics": {
                      "motorcycleCapacity": { "maxCapacity": 50 },
                      "zoneCapacities": []
                    }
                  }
                }
                """);
            }
            return JsonResponse(HttpStatusCode.NotFound, "{}");
        });

        using var http = new HttpClient(transport) { BaseAddress = new Uri("https://facility.test/") };
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.Authorization = "Bearer owner-jwt";
        var accessor = new HttpContextAccessor { HttpContext = ctx };
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Services:Key"] = "unit-test-service-key-at-least-32-chars"
        }).Build();

        var adapter = new PassPlanFacilityScopeAdapter(http, identity, repo, accessor, cfg);
        var scope = await adapter.ValidateOwnerDraftScopeAsync(
            SiteId,
            ZoneId,
            [PassVehicleType.CAR, PassVehicleType.MOTORCYCLE],
            [],
            CancellationToken.None);

        Assert.Equal(TenantId, scope.TenantId);
        Assert.Equal(UserId, scope.UserId);

        var unsupportedEx = await Assert.ThrowsAsync<PassPlanException>(() =>
            adapter.ValidateOwnerDraftScopeAsync(
                SiteId,
                ZoneId,
                [PassVehicleType.OVERSIZED],
                [],
                CancellationToken.None));
        Assert.Equal(422, unsupportedEx.StatusCode);
        Assert.Equal("UNSUPPORTED_LOT_SCOPE", unsupportedEx.Code);
    }

    [Fact]
    public async Task PassPlanFacilityScopeAdapter_InvalidJsonOrWrongTenant_MapsErrorsProperly()
    {
        await using var db = CreateInMemoryDb();
        db.Set<SearchFacilityProjectionRow>().Add(new SearchFacilityProjectionRow
        {
            SiteId = SiteId,
            TenantId = Guid.NewGuid(),
            SiteCode = "LOT1",
            Name = "Other Lot",
            Address = "Addr",
            SupportedVehicleTypes = ["CAR"],
            IsOpen = true,
            OperationalStatus = "OPEN"
        });
        await db.SaveChangesAsync();

        var repo = new PassPlanRepository(db);
        var identity = System.Reflection.DispatchProxy.Create<IOwnerIdentityClient, DelegateOwnerIdentityProxy>();
        ((DelegateOwnerIdentityProxy)identity).GetOwnerScopeAsyncFunc = (_, _) =>
            Task.FromResult(new OwnerIdentityScope(UserId, [TenantId]));

        using var transport = new PassPlanStubHttpMessageHandler(_ => JsonResponse(HttpStatusCode.OK, "not-valid-json"));
        using var http = new HttpClient(transport) { BaseAddress = new Uri("https://facility.test/") };
        var adapter = new PassPlanFacilityScopeAdapter(
            http,
            identity,
            repo,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            new ConfigurationBuilder().Build());

        var forbiddenEx = await Assert.ThrowsAsync<PassPlanException>(() =>
            adapter.ValidateOwnerSiteAsync(SiteId, CancellationToken.None));
        Assert.Equal(403, forbiddenEx.StatusCode);
        Assert.Equal("FORBIDDEN_SITE_SCOPE", forbiddenEx.Code);

        // With matching tenant + invalid structure JSON -> 503 FACILITY_UNAVAILABLE
        await using var db2 = CreateInMemoryDb();
        db2.Set<SearchFacilityProjectionRow>().Add(new SearchFacilityProjectionRow
        {
            SiteId = SiteId,
            TenantId = TenantId,
            SiteCode = "LOT1",
            Name = "Owned Lot",
            Address = "Addr",
            SupportedVehicleTypes = ["CAR"],
            IsOpen = true,
            OperationalStatus = "OPEN"
        });
        await db2.SaveChangesAsync();

        var adapter2 = new PassPlanFacilityScopeAdapter(
            http,
            identity,
            new PassPlanRepository(db2),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            new ConfigurationBuilder().Build());

        var unavailableEx = await Assert.ThrowsAsync<PassPlanException>(() =>
            adapter2.ValidateOwnerDraftScopeAsync(SiteId, null, [PassVehicleType.CAR], [], CancellationToken.None));
        Assert.Equal(503, unavailableEx.StatusCode);
        Assert.Equal("FACILITY_UNAVAILABLE", unavailableEx.Code);
    }

    [Fact]
    public async Task PassPlanFacilityScopeAdapter_UnauthenticatedIdentity_Throws401()
    {
        await using var db = CreateInMemoryDb();
        var repo = new PassPlanRepository(db);
        var identity = System.Reflection.DispatchProxy.Create<IOwnerIdentityClient, DelegateOwnerIdentityProxy>();
        ((DelegateOwnerIdentityProxy)identity).GetOwnerScopeAsyncFunc = (_, _) =>
            throw new ReservationGateException(401, "UNAUTHENTICATED", "Missing session.");

        using var transport = new PassPlanStubHttpMessageHandler(_ => JsonResponse(HttpStatusCode.OK, "{}"));
        using var http = new HttpClient(transport) { BaseAddress = new Uri("https://facility.test/") };
        var adapter = new PassPlanFacilityScopeAdapter(
            http,
            identity,
            repo,
            new HttpContextAccessor(),
            new ConfigurationBuilder().Build());

        var ex = await Assert.ThrowsAsync<PassPlanException>(() =>
            adapter.ValidateOwnerSiteAsync(SiteId, CancellationToken.None));
        Assert.Equal(401, ex.StatusCode);
        Assert.Equal("UNAUTHENTICATED", ex.Code);
    }
}
