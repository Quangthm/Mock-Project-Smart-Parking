using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SmartParking.OperationService.API;
using SmartParking.OperationService.API.Authentication;
using SmartParking.OperationService.API.Controllers;
using SmartParking.OperationService.API.Middleware;
using SmartParking.OperationService.Application;
using SmartParking.OperationService.Application.PassPlans.DTOs;
using SmartParking.OperationService.Application.PassPlans.Options;
using SmartParking.OperationService.Application.PassPlans.Ports;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Domain.Reservations;
using SmartParking.OperationService.Persistence;
using SmartParking.OperationService.Persistence.Repositories;

namespace SmartParking.OperationService.Tests.PassPlans;

public sealed class PassPlanHttpPipelineTests
{
    private static readonly Guid OwnerUserId = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000020");
    private static readonly Guid TenantId = Guid.Parse("bbbbbbbb-0000-4000-8000-000000000020");
    private static readonly Guid SiteId = Guid.Parse("cccccccc-0000-4000-8000-000000000020");
    private const string InternalKey = "operation-service-test-key-at-least-32-chars";

    [Fact]
    public async Task OperationHost_WithoutJwtValidationConfig_BootsAndChallengesProtectedPassPlansWith401()
    {
        var app = OperationHost.Build(
            [
                "--environment", "Testing",
                "--Services:Key", InternalKey,
                "--ConnectionStrings:Reservation", "Host=127.0.0.1;Port=1;Database=unused;Username=unused;Password=unused",
                "--Messaging:Enabled", "false",
                "--ReservationIntegration:Enabled", "false"
            ],
            builder => builder.WebHost.UseTestServer());

        await app.StartAsync();
        try
        {
            using var client = app.GetTestClient();

            var health = await client.GetAsync("/health");
            Assert.Equal(HttpStatusCode.OK, health.StatusCode);

            var unauthPlans = await client.GetAsync($"/api/v1/lots/{SiteId}/pass-plans");
            Assert.Equal(HttpStatusCode.Unauthorized, unauthPlans.StatusCode);
            var payload = await unauthPlans.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("UNAUTHENTICATED", payload.GetProperty("code").GetString());

            var unauthFreeze = await client.PostAsJsonAsync(
                "/internal/pass-plans/validate-and-freeze",
                new ValidateAndFreezePassPlanRequest(SiteId, Guid.NewGuid(), 1, "MONTHLY", "CAR"));
            Assert.Equal(HttpStatusCode.Unauthorized, unauthFreeze.StatusCode);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task HttpPipeline_EnforcesOwnerAndServiceJwtReplayHeaderAndPreservesLegacyInternalKey()
    {
        using var ownerRsa = RSA.Create(2048);
        using var serviceRsa = RSA.Create(2048);
        var ownerPublicPem = ownerRsa.ExportSubjectPublicKeyInfoPem();
        var servicePublicPem = serviceRsa.ExportSubjectPublicKeyInfoPem();

        var planIdHolder = new List<Guid>();
        var dbName = Guid.NewGuid().ToString("N");

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Services:Key"] = InternalKey,
            ["ReservationIntegration:Enabled"] = "true",
            ["ReservationIntegration:OperationsPublicKeyPem"] = servicePublicPem
        }).Build();

        using var host = await new HostBuilder().ConfigureWebHost(web => web.UseTestServer()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddSingleton<IConfiguration>(config);
                services.AddRouting();
                services.AddHttpContextAccessor();
                services.AddDbContext<ReservationDbContext>(opts =>
                    opts.UseInMemoryDatabase(dbName)
                        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
                services.AddScoped<IPassPlanRepository, PassPlanRepository>();
                services.AddApplicationServices();
                services.AddSingleton(Options.Create(new PassPlanDefaultsOptions()));
                services.AddSingleton(TimeProvider.System);
                services.AddScoped<ICurrentActor, HttpCurrentActor>();

                var scopeProxy = DispatchProxy.Create<IPassPlanFacilityScopePort, DelegatePassScopeProxy>();
                var typedScope = (DelegatePassScopeProxy)scopeProxy;
                typedScope.ValidateOwnerSiteAsyncFunc = (_, _) => Task.FromResult(new OwnerSiteScopeResult(OwnerUserId, TenantId, SiteId));
                typedScope.ValidateOwnerDraftScopeAsyncFunc = (_, _, _, _, _) => Task.FromResult(new OwnerSiteScopeResult(OwnerUserId, TenantId, SiteId));
                typedScope.SiteExistsAsyncFunc = (id, _) => Task.FromResult(id == SiteId);
                services.AddSingleton(scopeProxy);

                var paymentProxy = DispatchProxy.Create<IPaymentPassTermsPort, DelegatePaymentTermsProxy>();
                ((DelegatePaymentTermsProxy)paymentProxy).GetActiveFinancialTermsFunc = (_, planId, _) =>
                {
                    var effectivePlanId = planId ?? planIdHolder.FirstOrDefault();
                    if (effectivePlanId == Guid.Empty)
                    {
                        return Task.FromResult<IReadOnlyList<PaymentPassFinancialTermDto>>([]);
                    }

                    return Task.FromResult<IReadOnlyList<PaymentPassFinancialTermDto>>(
                    [
                        new PaymentPassFinancialTermDto(
                            Guid.Parse("11111111-1111-4000-8000-000000000001"),
                            SiteId,
                            effectivePlanId,
                            1,
                            1,
                            "MONTHLY",
                            "VND",
                            true,
                            [new PaymentPassVehiclePriceDto("CAR", 1_500_000m)],
                            new PaymentPassRefundPolicyDto(100, 50, 100, "HALF_UP_THOUSAND"),
                            DateTimeOffset.UtcNow)
                    ]);
                };
                services.AddSingleton(paymentProxy);

                services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddJwtBearer(options =>
                    {
                        using var rsa = RSA.Create();
                        rsa.ImportFromPem(ownerPublicPem);
                        options.MapInboundClaims = false;
                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuer = true,
                            ValidIssuer = "SmartParking.UserService",
                            ValidateAudience = true,
                            ValidAudience = "SmartParking",
                            ValidateIssuerSigningKey = true,
                            IssuerSigningKey = new RsaSecurityKey(rsa.ExportParameters(false)),
                            ValidateLifetime = true,
                            NameClaimType = "sub",
                            RoleClaimType = "role"
                        };
                        options.Events = new JwtBearerEvents
                        {
                            OnChallenge = async context =>
                            {
                                context.HandleResponse();
                                await ApiErrors.WriteAsync(context.HttpContext, 401, "UNAUTHENTICATED",
                                    "Authentication is required.", ct: context.HttpContext.RequestAborted);
                            },
                            OnForbidden = async context =>
                            {
                                await ApiErrors.WriteAsync(context.HttpContext, 403, "FORBIDDEN",
                                    "Access is denied.", ct: context.HttpContext.RequestAborted);
                            }
                        };
                    });

                services.AddPassPlanServiceAuthentication(config);
                services.AddAuthorization();
                services.AddPassPlanOwnerPolicy();
                services.AddExceptionHandler<ReservationExceptionHandler>();
                ApiErrors.Configure(services);
                services.AddControllers().AddApplicationPart(typeof(PassPlansController).Assembly);
            })
            .Configure(app =>
            {
                ApiErrors.Use(app);
                app.UseRouting();
                app.UseMiddleware<InternalServiceKeyMiddleware>();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            })).StartAsync();

        using var client = host.GetTestClient();

        // Seed timezone policy row
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();
            db.Set<OperationalPolicyRow>().Add(new OperationalPolicyRow
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                SiteId = SiteId,
                Version = 1,
                IsActive = true,
                CancellationTimezone = "Asia/Ho_Chi_Minh"
            });
            await db.SaveChangesAsync();
        }

        var ownerToken = CreateOwnerToken(ownerRsa);

        // 1. Unauthenticated owner endpoint -> 401; non-owner JWT -> 403
        var unauthResponse = await client.GetAsync($"/api/v1/lots/{SiteId}/pass-plans");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthResponse.StatusCode);

        using var driverReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/lots/{SiteId}/pass-plans");
        driverReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateUserToken(ownerRsa, "driver"));
        var driverResp = await client.SendAsync(driverReq);
        Assert.Equal(HttpStatusCode.Forbidden, driverResp.StatusCode);

        // 2. A1 GET pass plans
        using var getPlansReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/lots/{SiteId}/pass-plans");
        getPlansReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var getPlansResp = await client.SendAsync(getPlansReq);
        Assert.Equal(HttpStatusCode.OK, getPlansResp.StatusCode);

        // 3. A2 PUT draft -> validation error (400 VALIDATION_ERROR) then valid 200
        using var invalidDraftReq = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/lots/{SiteId}/pass-plans/MONTHLY/draft");
        invalidDraftReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        invalidDraftReq.Content = JsonContent.Create(new
        {
            zoneId = (Guid?)null,
            vehicleTypes = new[] { "CAR" },
            ordinaryCoverageRules = new { dayCovered = true, nightCovered = false, excludedZoneIds = Array.Empty<Guid>() },
            isAvailableOnly = false
        });
        var invalidDraftResp = await client.SendAsync(invalidDraftReq);
        Assert.Equal(HttpStatusCode.BadRequest, invalidDraftResp.StatusCode);
        var invalidJson = await invalidDraftResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("VALIDATION_ERROR", invalidJson.GetProperty("code").GetString());

        using var validDraftReq = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/lots/{SiteId}/pass-plans/MONTHLY/draft");
        validDraftReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        validDraftReq.Content = JsonContent.Create(new
        {
            zoneId = (Guid?)null,
            vehicleTypes = new[] { "CAR" },
            ordinaryCoverageRules = new { dayCovered = true, nightCovered = true, excludedZoneIds = Array.Empty<Guid>() },
            isAvailableOnly = true
        });
        var validDraftResp = await client.SendAsync(validDraftReq);
        Assert.Equal(HttpStatusCode.OK, validDraftResp.StatusCode);
        var draftBody = await validDraftResp.Content.ReadFromJsonAsync<JsonElement>();
        var planId = draftBody.GetProperty("data").GetProperty("planId").GetGuid();
        planIdHolder.Add(planId);

        // 4. A3 Publish + Replay header + Conflict
        using var pubReq1 = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/lots/{SiteId}/pass-plans/MONTHLY/publish");
        pubReq1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        pubReq1.Headers.Add("Idempotency-Key", "http-pub-1");
        pubReq1.Content = JsonContent.Create(new { publishNote = "v1" });
        var pubResp1 = await client.SendAsync(pubReq1);
        Assert.Equal(HttpStatusCode.OK, pubResp1.StatusCode);
        Assert.False(pubResp1.Headers.Contains("Idempotency-Replayed"));

        using var pubReq2 = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/lots/{SiteId}/pass-plans/MONTHLY/publish");
        pubReq2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        pubReq2.Headers.Add("Idempotency-Key", "http-pub-1");
        pubReq2.Content = JsonContent.Create(new { publishNote = "v1" });
        var pubResp2 = await client.SendAsync(pubReq2);
        Assert.Equal(HttpStatusCode.OK, pubResp2.StatusCode);
        Assert.True(pubResp2.Headers.TryGetValues("Idempotency-Replayed", out var replayHeader));
        Assert.Equal("true", Assert.Single(replayHeader));

        using var pubConflict = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/lots/{SiteId}/pass-plans/MONTHLY/publish");
        pubConflict.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        pubConflict.Headers.Add("Idempotency-Key", "http-pub-1");
        pubConflict.Content = JsonContent.Create(new { publishNote = "different" });
        var pubConflictResp = await client.SendAsync(pubConflict);
        Assert.Equal(HttpStatusCode.Conflict, pubConflictResp.StatusCode);

        // 5. A4 Enablement
        using var enableReq = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/lots/{SiteId}/pass-plans/MONTHLY/enablement");
        enableReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        enableReq.Content = JsonContent.Create(new { isEnabled = true, expectedPlanRevision = 1 });
        var enableResp = await client.SendAsync(enableReq);
        Assert.Equal(HttpStatusCode.OK, enableResp.StatusCode);

        // 6. B1 Anonymous offers
        var offersResp = await client.GetAsync($"/api/v1/sites/{SiteId}/pass-offers?vehicleType=CAR");
        Assert.Equal(HttpStatusCode.OK, offersResp.StatusCode);
        var offersJson = await offersResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Single(offersJson.GetProperty("data").GetProperty("offers").EnumerateArray());

        // 7. B2 Internal validate-and-freeze security checks
        var freezePayload = new ValidateAndFreezePassPlanRequest(SiteId, planId, 1, "MONTHLY", "CAR");

        // X-Service-Key alone MUST be rejected (401 UNAUTHENTICATED)
        using var legacyKeyReq = new HttpRequestMessage(HttpMethod.Post, "/internal/pass-plans/validate-and-freeze");
        legacyKeyReq.Headers.Add("X-Service-Key", InternalKey);
        legacyKeyReq.Content = JsonContent.Create(freezePayload);
        var legacyKeyResp = await client.SendAsync(legacyKeyReq);
        Assert.Equal(HttpStatusCode.Unauthorized, legacyKeyResp.StatusCode);

        // RS256 with wrong permission MUST be rejected (403 FORBIDDEN)
        using var wrongPermReq = new HttpRequestMessage(HttpMethod.Post, "/internal/pass-plans/validate-and-freeze");
        wrongPermReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateServiceToken(serviceRsa, "wrong:perm"));
        wrongPermReq.Content = JsonContent.Create(freezePayload);
        var wrongPermResp = await client.SendAsync(wrongPermReq);
        Assert.Equal(HttpStatusCode.Forbidden, wrongPermResp.StatusCode);

        // Valid RS256 Service JWT -> 200 OK direct snapshot JSON
        using var validFreezeReq = new HttpRequestMessage(HttpMethod.Post, "/internal/pass-plans/validate-and-freeze");
        validFreezeReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateServiceToken(serviceRsa, PassPlanServiceAuthentication.Permission));
        validFreezeReq.Content = JsonContent.Create(freezePayload);
        var validFreezeResp = await client.SendAsync(validFreezeReq);
        Assert.Equal(HttpStatusCode.OK, validFreezeResp.StatusCode);
        var frozen = await validFreezeResp.Content.ReadFromJsonAsync<FrozenPassPlanSnapshotDto>();
        Assert.NotNull(frozen);
        Assert.Equal(planId, frozen!.PlanId);
        Assert.Equal(1, frozen.PlanRevision);
        Assert.Equal("CAR", frozen.VehicleType);

        // 8. Legacy /internal/structure route STILL enforces X-Service-Key
        var legacyStructureNoKey = await client.GetAsync($"/internal/structure?siteId={SiteId}");
        Assert.Equal(HttpStatusCode.Unauthorized, legacyStructureNoKey.StatusCode);

        // 9. Empty JSON body on A3 publish succeeds cleanly
        using var emptyDraftReq = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/lots/{SiteId}/pass-plans/MONTHLY/draft");
        emptyDraftReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        emptyDraftReq.Content = JsonContent.Create(new
        {
            zoneId = (Guid?)null,
            vehicleTypes = new[] { "CAR" },
            ordinaryCoverageRules = new { dayCovered = true, nightCovered = true, excludedZoneIds = Array.Empty<Guid>() },
            isAvailableOnly = true
        });
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(emptyDraftReq)).StatusCode);

        using var emptyPublishReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/lots/{SiteId}/pass-plans/MONTHLY/publish");
        emptyPublishReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        emptyPublishReq.Content = new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(emptyPublishReq)).StatusCode);
    }

    private static string CreateOwnerToken(RSA rsa) => CreateUserToken(rsa, "owner");

    private static string CreateUserToken(RSA rsa, string role)
    {
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            "SmartParking.UserService",
            "SmartParking",
            [
                new Claim("sub", OwnerUserId.ToString("D")),
                new Claim("role", role)
            ],
            now,
            now.AddMinutes(5),
            new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string CreateServiceToken(RSA rsa, string permission)
    {
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            "SmartParking.OperationService",
            "SmartParking.OperationService",
            [
                new Claim("sub", "SmartParking.OperationService"),
                new Claim("sender", "SmartParking.OperationService"),
                new Claim("permission", permission),
                new Claim("jti", Guid.NewGuid().ToString("N"))
            ],
            now,
            now.AddMinutes(1),
            new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
