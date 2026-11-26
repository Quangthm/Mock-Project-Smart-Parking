using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using SmartParking.PaymentService.API.Authentication;
using SmartParking.PaymentService.API.Controllers;
using SmartParking.PaymentService.API.Middleware;
using SmartParking.PaymentService.Application;
using SmartParking.PaymentService.Application.Common.Interfaces;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Application.DTOs.PassPlans;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Exceptions;
using SmartParking.PaymentService.Infrastructure.Adapters;
using SmartParking.PaymentService.Persistence;
using SmartParking.PaymentService.Persistence.Repositories;

namespace SmartParking.PaymentService.Tests.PassPlans;

public sealed class PassPlanPaymentHttpPipelineTests
{
    private static readonly Guid OwnerUserId = Guid.Parse("11111111-0000-4000-8000-000000000030");
    private static readonly Guid DriverUserId = Guid.Parse("11111111-0000-4000-8000-000000000031");
    private static readonly Guid TenantId = Guid.Parse("22222222-0000-4000-8000-000000000030");
    private static readonly Guid SiteId = Guid.Parse("33333333-0000-4000-8000-000000000030");
    private static readonly Guid PlanId = Guid.Parse("44444444-0000-4000-8000-000000000030");

    [Fact]
    public async Task HttpPipeline_EnforcesUserJwtOwnerPolicyPreservesDevBearerAndSecuresInternalRoutes()
    {
        using var userRsa = RSA.Create(2048);
        using var serviceRsa = RSA.Create(2048);
        var jwkParams = userRsa.ExportParameters(false);
        var jwkJson = JsonSerializer.Serialize(new
        {
            keys = new[]
            {
                new
                {
                    kty = "RSA",
                    use = "sig",
                    alg = "RS256",
                    kid = "owner-key-1",
                    n = Base64UrlEncoder.Encode(jwkParams.Modulus!),
                    e = Base64UrlEncoder.Encode(jwkParams.Exponent!)
                }
            }
        });

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JwtValidation:Issuer"] = "SmartParking.UserService",
            ["JwtValidation:Audience"] = "SmartParking",
            ["JwtValidation:JwksUri"] = "https://user.test/.well-known/jwks.json",
            ["ReservationIntegration:Enabled"] = "true",
            ["ReservationIntegration:OperationsPublicKeyPem"] = serviceRsa.ExportSubjectPublicKeyInfoPem()
        }).Build();

        var dbName = Guid.NewGuid().ToString("N");
        using var jwksTransport = new PaymentStubHttpHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jwkJson, Encoding.UTF8, "application/json")
            });

        using var host = await new HostBuilder()
            .UseDefaultServiceProvider(options =>
            {
                options.ValidateScopes = false;
                options.ValidateOnBuild = false;
            })
            .ConfigureWebHost(web => web
            .UseEnvironment("Development")
            .UseTestServer()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddExceptionHandler<PaymentExceptionHandler>();
                services.AddProblemDetails();
                services.AddSingleton<IConfiguration>(config);
                services.AddRouting();
                services.AddHttpContextAccessor();
                services.AddDbContext<PaymentDbContext>(opts =>
                    opts.UseInMemoryDatabase(dbName)
                        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
                services.AddApplicationServices();
                services.AddScoped<ICurrentActor, CurrentActorAdapter>();
                services.AddScoped<ISubscriptionFinancialTermRepository, SubscriptionFinancialTermRepository>();
                services.AddScoped<ISubscriptionClosureCompensationRepository, SubscriptionClosureCompensationRepository>();
                services.AddScoped<IPassRevenueRepository, PassRevenueRepository>();
                services.AddScoped<IPaymentOrderRepository, PaymentOrderRepository>();

                var scopeProxy = DispatchProxy.Create<IOwnerSiteAccessPort, DelegateOwnerSiteAccessPortProxy>();
                ((DelegateOwnerSiteAccessPortProxy)scopeProxy).ValidateOwnerSiteAsyncFunc = (id, _) =>
                {
                    if (id != SiteId)
                        throw new PaymentDomainException("SITE_NOT_FOUND", "Parking lot not found.", "siteId");
                    return Task.FromResult(new OwnerSiteScopeResult(OwnerUserId, TenantId, SiteId));
                };
                services.AddSingleton(scopeProxy);

                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = DevAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme = DevAuthenticationHandler.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, DevAuthenticationHandler>(DevAuthenticationHandler.SchemeName, null);

                services.AddUserJwtAuthentication(config, new TestHostEnvironment("Development"));
                services.AddHttpClient("SmartParking.Payment.Jwks")
                    .ConfigurePrimaryHttpMessageHandler(() => jwksTransport);
                services.AddOperationsServiceAuthentication(config);

                services.AddControllers().AddApplicationPart(typeof(PassFinancialTermsController).Assembly);
            })
            .Configure(app =>
            {
                app.UseExceptionHandler();
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                    endpoints.MapGet("/test/legacy-dev", (HttpContext ctx) =>
                        Results.Ok(new { scheme = ctx.User.Identity?.AuthenticationType, authenticated = ctx.User.Identity?.IsAuthenticated }))
                        .RequireAuthorization();
                });
            })).StartAsync();

        using var client = host.GetTestClient();

        // 1. Legacy endpoint continues using DevBearer untouched
        using var devReq = new HttpRequestMessage(HttpMethod.Get, "/test/legacy-dev");
        devReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "dev-only-dummy-token");
        var devResp = await client.SendAsync(devReq);
        Assert.Equal(HttpStatusCode.OK, devResp.StatusCode);
        var devJson = await devResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("DevBearer", devJson.GetProperty("scheme").GetString());

        // 2. Owner endpoints REJECT DevBearer dummy token with 401 UNAUTHENTICATED PaymentService envelope
        using var dummyOnOwnerReq = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/payments/owners/lots/{SiteId}/pass-revenue-summary");
        dummyOnOwnerReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "dev-only-dummy-token");
        var dummyOnOwnerResp = await client.SendAsync(dummyOnOwnerReq);
        Assert.Equal(HttpStatusCode.Unauthorized, dummyOnOwnerResp.StatusCode);
        var unauthJson = await dummyOnOwnerResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(unauthJson.GetProperty("success").GetBoolean());
        Assert.Equal("UNAUTHENTICATED", unauthJson.GetProperty("code").GetString());

        // 3. Driver RS256 JWT gets 403 FORBIDDEN PaymentService envelope
        using var driverReq = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/payments/owners/lots/{SiteId}/pass-revenue-summary");
        driverReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateUserToken(userRsa, DriverUserId, "driver"));
        var driverResp = await client.SendAsync(driverReq);
        Assert.Equal(HttpStatusCode.Forbidden, driverResp.StatusCode);
        var forbidJson = await driverResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("FORBIDDEN", forbidJson.GetProperty("code").GetString());

        // 4. Lowercase "owner" RS256 JWT accesses A5 + Idempotency-Replayed header & A6
        var ownerJwt = CreateUserToken(userRsa, OwnerUserId, "owner");
        var termsBody = new
        {
            productType = "MONTHLY",
            planRevision = 1,
            currency = "VND",
            vehiclePrices = new[] { new { vehicleType = "CAR", fullPeriodPrice = 1_500_000m } },
            refundPolicy = new
            {
                voluntaryBeforeStartPercent = 100,
                voluntaryInPeriodPercent = 50,
                facilityClosurePercent = 100,
                roundingMode = "HALF_UP_THOUSAND"
            }
        };

        using var putTerms1 = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/v1/pricing/lots/{SiteId}/pass-plans/{PlanId}/financial-terms");
        putTerms1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerJwt);
        putTerms1.Headers.Add("Idempotency-Key", "http-fin-1");
        putTerms1.Content = JsonContent.Create(termsBody);
        var putResp1 = await client.SendAsync(putTerms1);
        Assert.Equal(HttpStatusCode.OK, putResp1.StatusCode);

        using var putTermsReplay = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/v1/pricing/lots/{SiteId}/pass-plans/{PlanId}/financial-terms");
        putTermsReplay.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerJwt);
        putTermsReplay.Headers.Add("Idempotency-Key", "http-fin-1");
        putTermsReplay.Content = JsonContent.Create(termsBody);
        var putReplayResp = await client.SendAsync(putTermsReplay);
        Assert.Equal(HttpStatusCode.OK, putReplayResp.StatusCode);
        Assert.True(putReplayResp.Headers.TryGetValues("Idempotency-Replayed", out var replayValues));
        Assert.Equal("true", Assert.Single(replayValues));

        using var revReq = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/payments/owners/lots/{SiteId}/pass-revenue-summary?productType=MONTHLY");
        revReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerJwt);
        var revResp = await client.SendAsync(revReq);
        Assert.Equal(HttpStatusCode.OK, revResp.StatusCode);

        // 5. Internal B3 & B4 enforce OperationsService permissions cleanly
        var noAuthB3 = await client.GetAsync($"/internal/pass-financial-terms?siteId={SiteId}");
        Assert.Equal(HttpStatusCode.Unauthorized, noAuthB3.StatusCode);

        // Wrong permission rejected with 403 FORBIDDEN PaymentService envelope
        using var wrongScopeB3 = new HttpRequestMessage(
            HttpMethod.Get,
            $"/internal/pass-financial-terms?siteId={SiteId}");
        wrongScopeB3.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateServiceToken(serviceRsa, OperationsServiceAuthentication.PassClosuresCompensatePermission));
        var wrongScopeResp = await client.SendAsync(wrongScopeB3);
        Assert.Equal(HttpStatusCode.Forbidden, wrongScopeResp.StatusCode);
        var wrongScopeJson = await wrongScopeResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("FORBIDDEN", wrongScopeJson.GetProperty("code").GetString());

        // Proper B3 read & quote tokens succeed
        using var readB3 = new HttpRequestMessage(HttpMethod.Get, $"/internal/pass-financial-terms?siteId={SiteId}");
        readB3.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateServiceToken(serviceRsa, OperationsServiceAuthentication.PassFinancialTermsReadPermission));
        var readB3Resp = await client.SendAsync(readB3);
        Assert.Equal(HttpStatusCode.OK, readB3Resp.StatusCode);
        var readB3Json = await readB3Resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Single(readB3Json.GetProperty("data").EnumerateArray());

        using var quoteB3 = new HttpRequestMessage(HttpMethod.Post, "/internal/pass-financial-terms/quote-snapshot");
        quoteB3.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateServiceToken(serviceRsa, OperationsServiceAuthentication.PassFinancialTermsQuotePermission));
        quoteB3.Content = JsonContent.Create(new QuotePassFinancialTermsRequest(SiteId, PlanId, 1, 1, "MONTHLY", "CAR"));
        var quoteB3Resp = await client.SendAsync(quoteB3);
        Assert.Equal(HttpStatusCode.OK, quoteB3Resp.StatusCode);

        // B4 compensate endpoint succeeds with PassClosuresCompensate permission
        var orderId = Guid.NewGuid();
        var passId = Guid.NewGuid();
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
            db.PaymentOrders.Add(new PaymentOrder
            {
                Id = orderId,
                TenantId = TenantId,
                SiteId = SiteId,
                OrderCode = "HTTP-PASS-1",
                OrderType = OrderType.MONTHLY_PASS_FEE,
                ReferenceType = ReferenceType.MONTHLY_PASS,
                ReferenceId = passId,
                Amount = 1_500_000m,
                FinalAmount = 1_500_000m,
                Status = PaymentOrderStatus.PAID
            });
            await db.SaveChangesAsync();
        }

        var periodStart = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        using var compReq = new HttpRequestMessage(HttpMethod.Post, "/internal/pass-closures/compensate");
        compReq.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateServiceToken(serviceRsa, OperationsServiceAuthentication.PassClosuresCompensatePermission));
        compReq.Content = JsonContent.Create(new CompensatePassClosureRequest(
            "http-closure-1",
            Guid.NewGuid(),
            "TEMPORARY",
            SiteId,
            TenantId,
            passId,
            orderId,
            periodStart,
            periodStart.AddDays(30),
            [new ClosureIntervalDto(periodStart.AddDays(1), periodStart.AddDays(4))]));
        var compResp = await client.SendAsync(compReq);
        Assert.Equal(HttpStatusCode.OK, compResp.StatusCode);
        var compJson = await compResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(150_000m, compJson.GetProperty("data").GetProperty("newlyCompensatedAmount").GetDecimal());

        // 6. Malformed null fields in JSON payloads return 400 BadRequest (never 500)
        using var badA5 = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/v1/pricing/lots/{SiteId}/pass-plans/{PlanId}/financial-terms");
        badA5.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerJwt);
        badA5.Content = new StringContent(
            """{"productType":null,"planRevision":1,"currency":null,"vehiclePrices":[null],"refundPolicy":null}""",
            Encoding.UTF8,
            "application/json");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(badA5)).StatusCode);

        using var badB3 = new HttpRequestMessage(HttpMethod.Post, "/internal/pass-financial-terms/quote-snapshot");
        badB3.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateServiceToken(serviceRsa, OperationsServiceAuthentication.PassFinancialTermsQuotePermission));
        badB3.Content = new StringContent(
            $$"""{"siteId":"{{SiteId}}","planId":"{{PlanId}}","planRevision":1,"expectedFinancialTermRevision":1,"productType":null,"vehicleType":null}""",
            Encoding.UTF8,
            "application/json");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(badB3)).StatusCode);

        using var badB4 = new HttpRequestMessage(HttpMethod.Post, "/internal/pass-closures/compensate");
        badB4.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateServiceToken(serviceRsa, OperationsServiceAuthentication.PassClosuresCompensatePermission));
        badB4.Content = new StringContent(
            $$"""{"idempotencyKey":"bad-null","closureId":"{{Guid.NewGuid()}}","closureType":null,"siteId":"{{SiteId}}","tenantId":"{{TenantId}}","passId":"{{passId}}","paymentOrderId":"{{orderId}}","paidPeriodStart":"2026-11-01T00:00:00Z","paidPeriodEnd":"2026-12-01T00:00:00Z","unavailableIntervals":[null]}""",
            Encoding.UTF8,
            "application/json");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(badB4)).StatusCode);
    }

    [Fact]
    public async Task PaymentExceptionHandler_MapsPostgres23505UniqueViolation_To409IdempotencyConflict()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        using var responseBody = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = responseBody;
        var pgEx = new PostgresException("duplicate key value violates unique constraint", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation);
        var dbEx = new DbUpdateException("Update failed", pgEx);

        var handler = new PaymentExceptionHandler(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PaymentExceptionHandler>.Instance);

        Assert.True(await handler.TryHandleAsync(context, dbEx, default));

        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var payload = await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body);
        Assert.Equal("IDEMPOTENCY_CONFLICT", payload.GetProperty("code").GetString());
        Assert.DoesNotContain("constraint", payload.GetProperty("message").GetString()!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UserJwt_WhenJwksUnavailable_Returns503AuthenticationKeysUnavailable()
    {
        using var rsa = RSA.Create(2048);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JwtValidation:Issuer"] = "SmartParking.UserService",
            ["JwtValidation:Audience"] = "SmartParking",
            ["JwtValidation:JwksUri"] = "https://user.test/.well-known/jwks.json"
        }).Build();

        using var failingJwks = new PaymentStubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        using var host = await new HostBuilder().ConfigureWebHost(web => web
            .UseEnvironment("Production")
            .UseTestServer()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddExceptionHandler<PaymentExceptionHandler>();
                services.AddProblemDetails();
                services.AddRouting();
                services.AddUserJwtAuthentication(config, new TestHostEnvironment("Production"));
                services.AddHttpClient("SmartParking.Payment.Jwks")
                    .ConfigurePrimaryHttpMessageHandler(() => failingJwks);
            })
            .Configure(app =>
            {
                app.UseExceptionHandler();
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/owner-check", () => Results.Ok())
                        .RequireAuthorization(UserJwtAuthentication.OwnerLotManagementPolicy);
                });
            })).StartAsync();

        using var client = host.GetTestClient();
        using var req = new HttpRequestMessage(HttpMethod.Get, "/owner-check");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateUserToken(rsa, OwnerUserId, "owner"));
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("AUTHENTICATION_KEYS_UNAVAILABLE", body.GetProperty("code").GetString());
    }

    private static string CreateUserToken(RSA rsa, Guid userId, string role)
    {
        var now = DateTime.UtcNow;
        var creds = new SigningCredentials(new RsaSecurityKey(rsa) { KeyId = "owner-key-1" }, SecurityAlgorithms.RsaSha256);
        var token = new JwtSecurityToken(
            "SmartParking.UserService",
            "SmartParking",
            [
                new Claim("sub", userId.ToString("D")),
                new Claim("role", role)
            ],
            now,
            now.AddMinutes(5),
            creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string CreateServiceToken(RSA rsa, string permission)
    {
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            "SmartParking.OperationService",
            "SmartParking.PaymentService",
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
