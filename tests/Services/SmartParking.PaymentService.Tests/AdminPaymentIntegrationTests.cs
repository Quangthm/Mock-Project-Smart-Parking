using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SmartParking.PaymentService.API.Authentication;
using SmartParking.PaymentService.API.Controllers;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Application.Integrations;
using SmartParking.PaymentService.Infrastructure.Adapters.Outbound;
using SmartParking.PaymentService.Infrastructure.Options;
using SmartParking.PaymentService.Infrastructure.Services;
using SmartParking.PaymentService.Tests.TestDoubles;

namespace SmartParking.PaymentService.Tests;

/// <summary>
/// SPARK-340 Phase 4 preparation: regression coverage for PayOS mode isolation,
/// zero-HTTP guarantees, probe status wiring, secret leakage, admin authorization
/// and the API-layer error mapping.
/// </summary>
public class AdminPaymentIntegrationTests
{
    private const string Issuer = "SmartParking.UserService";
    private const string Audience = "SmartParking";

    private const string SuccessBody = """
        {"code":"00","desc":"success","data":{"checkoutUrl":"https://pay.payos.vn/web/test","qrCode":"QR","accountNumber":"0123","accountName":"SMARTPARKING","bin":"970422","paymentLinkId":"LINK-1"}}
        """;

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private sealed class RecordingHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private static PaymentIntegrationSettingsStore CreateLiveStore(
        string clientId = "live-client-1234",
        string apiKey = "live-api-key-secret",
        string checksumKey = "live-checksum-key-secret",
        string baseUrl = "https://api-merchant.payos.vn")
    {
        return new PaymentIntegrationSettingsStore(Options.Create(new PayOsOptions
        {
            Mode = PaymentIntegrationSettingsStore.LiveMode,
            BaseUrl = baseUrl,
            LiveClientId = clientId,
            LiveApiKey = apiKey,
            LiveChecksumKey = checksumKey
        }));
    }

    private static PayOsAdapter CreateAdapter(PaymentIntegrationSettingsStore store, RecordingHandler handler)
    {
        return new PayOsAdapter(
            new HttpClient(handler),
            Options.Create(new PayOsOptions()),
            NullLogger<PayOsAdapter>.Instance,
            store, CheckoutBindingFixture.Create());
    }

    private static PaymentInitiateRequest CreateRequest()
    {
        return new PaymentInitiateRequest(
            TransactionId: Guid.NewGuid(),
            OrderCode: 20261113001,
            AmountVnd: 25000,
            Description: "Thanh toan dat cho",
            ReturnUrl: "https://example.com/return",
            CancelUrl: "https://example.com/cancel",
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(15));
    }

    // ---------------------------------------------------------------------
    // Finding 6: LIVE without live credentials must fail closed, no HTTP.
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("", "", "")]
    [InlineData("replace-with-client-id", "some-api-key", "some-checksum-key")]
    [InlineData("mock-client-id", "some-api-key", "some-checksum-key")]
    public async Task LiveMode_WithMissingOrMockCredentials_FailsClosedWithoutOutboundHttp(
        string clientId, string apiKey, string checksumKey)
    {
        var store = CreateLiveStore(clientId, apiKey, checksumKey);
        var handler = new RecordingHandler(HttpStatusCode.OK, SuccessBody);
        var adapter = CreateAdapter(store, handler);

        var result = await adapter.InitiatePaymentAsync(CreateRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal("PAYOS_CONFIGURATION_MISSING", result.ErrorCode);
        Assert.Empty(handler.Requests);
    }

    // ---------------------------------------------------------------------
    // Finding 7: FIXTURE always emulates locally.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task FixtureMode_EmulatesSuccessWithoutOutboundHttp()
    {
        var store = new PaymentIntegrationSettingsStore(Options.Create(new PayOsOptions
        {
            Mode = PaymentIntegrationSettingsStore.FixtureMode
        }));
        var handler = new RecordingHandler(HttpStatusCode.OK, SuccessBody);
        var adapter = CreateAdapter(store, handler);

        var result = await adapter.InitiatePaymentAsync(CreateRequest());

        Assert.True(result.IsSuccess);
        Assert.StartsWith("http://localhost:5173/payment/success?transactionId=", result.CheckoutUrl);
        Assert.Empty(handler.Requests);
    }

    // ---------------------------------------------------------------------
    // Finding 8: outbound base URL follows the store's current BaseUrl.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task OutboundBaseUrl_AfterSavingNewStoreBaseUrl_IsUsedForRequest()
    {
        var store = CreateLiveStore();
        store.Save(PaymentIntegrationSettingsStore.LiveMode, "https://payos.stub.local/prefix", null, null, null);

        var handler = new RecordingHandler(HttpStatusCode.OK, SuccessBody);
        var adapter = CreateAdapter(store, handler);

        var result = await adapter.InitiatePaymentAsync(CreateRequest());

        Assert.True(result.IsSuccess);
        var request = Assert.Single(handler.Requests);
        Assert.StartsWith("https://payos.stub.local/prefix/", request.ToString());
        Assert.Contains("v2/payment-requests", request.AbsolutePath);
    }

    // ---------------------------------------------------------------------
    // Finding 9: MarkProbeResult wiring into the GET status.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ProbeResult_AfterSuccessfulRealCall_IsHealthyWithLastTestedAt()
    {
        var store = CreateLiveStore();
        var handler = new RecordingHandler(HttpStatusCode.OK, SuccessBody);
        var adapter = CreateAdapter(store, handler);
        var queryHandler = new GetPaymentIntegrationQueryHandler(store);

        var before = await queryHandler.Handle(new GetPaymentIntegrationQuery(), CancellationToken.None);
        Assert.Equal(PaymentIntegrationSettingsStore.StatusUntested, before.Status);

        var result = await adapter.InitiatePaymentAsync(CreateRequest());
        Assert.True(result.IsSuccess);
        Assert.Single(handler.Requests);

        var after = await queryHandler.Handle(new GetPaymentIntegrationQuery(), CancellationToken.None);
        Assert.Equal(PaymentIntegrationSettingsStore.StatusHealthy, after.Status);
        Assert.NotNull(after.LastTestedAt);
    }

    [Fact]
    public async Task ProbeResult_AfterFailedRealCall_IsDegraded()
    {
        var store = CreateLiveStore();
        var handler = new RecordingHandler(HttpStatusCode.InternalServerError, "{}");
        var adapter = CreateAdapter(store, handler);
        var queryHandler = new GetPaymentIntegrationQueryHandler(store);

        var result = await adapter.InitiatePaymentAsync(CreateRequest());
        Assert.False(result.IsSuccess);
        Assert.Single(handler.Requests);

        var after = await queryHandler.Handle(new GetPaymentIntegrationQuery(), CancellationToken.None);
        Assert.Equal(PaymentIntegrationSettingsStore.StatusDegraded, after.Status);
        Assert.NotNull(after.LastTestedAt);
    }

    // ---------------------------------------------------------------------
    // Finding 10: DTOs never expose raw credential values.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task IntegrationDtos_NeverSerializeRawApiKeyOrChecksumKey()
    {
        const string clientId = "live-client-9876";
        const string apiKey = "api-key-plaintext-value";
        const string checksumKey = "checksum-key-plaintext-value";
        var store = CreateLiveStore(clientId, apiKey, checksumKey);

        var query = await new GetPaymentIntegrationQueryHandler(store)
            .Handle(new GetPaymentIntegrationQuery(), CancellationToken.None);

        Assert.True(query.ApiKeyConfigured);
        Assert.True(query.ChecksumKeyConfigured);
        Assert.Equal("****9876", query.ClientIdMasked);

        var queryJson = JsonSerializer.Serialize(query, WebJson);
        Assert.DoesNotContain(apiKey, queryJson);
        Assert.DoesNotContain(checksumKey, queryJson);
        Assert.DoesNotContain(clientId, queryJson);

        var updateHandler = new UpdatePaymentIntegrationCommandHandler(
            store,
            new FakeCurrentActor { UserId = Guid.NewGuid() },
            NullLogger<UpdatePaymentIntegrationCommandHandler>.Instance);

        var update = await updateHandler.Handle(new UpdatePaymentIntegrationCommand(
            PaymentIntegrationSettingsStore.LiveMode,
            "https://api-merchant.payos.vn",
            clientId,
            apiKey,
            checksumKey), CancellationToken.None);

        Assert.True(update.Data.ApiKeyConfigured);
        Assert.True(update.Data.ChecksumKeyConfigured);
        Assert.Equal("****9876", update.Data.ClientIdMasked);

        var updateJson = JsonSerializer.Serialize(update.Data, WebJson);
        Assert.DoesNotContain(apiKey, updateJson);
        Assert.DoesNotContain(checksumKey, updateJson);
        Assert.DoesNotContain(clientId, updateJson);
    }

    // ---------------------------------------------------------------------
    // Finding 11: real controller authorized through the UserServiceJwt scheme.
    // ---------------------------------------------------------------------

    private static IHost BuildAdminHost(RSA rsa)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtValidation:Issuer"] = Issuer,
                ["JwtValidation:Audience"] = Audience,
                ["JwtValidation:PublicKeyPem"] = rsa.ExportSubjectPublicKeyInfoPem()
            })
            .Build();

        var store = CreateLiveStore();

        var builder = new HostBuilder().ConfigureWebHost(web => web
            .UseTestServer()
            .UseEnvironment("Production")
            .ConfigureServices(services =>
            {
                services.AddSingleton<IConfiguration>(configuration);
                services.AddRouting();
                services.AddControllers()
                    .AddApplicationPart(typeof(AdminPaymentIntegrationController).Assembly);
                services.AddMediatR(cfg =>
                    cfg.RegisterServicesFromAssembly(typeof(GetPaymentIntegrationQuery).Assembly));
                services.AddUserServiceJwtAuthentication(configuration);
                services.AddSingleton<IPaymentIntegrationSettings>(store);
                services.AddLogging();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            }));

        return builder.StartAsync().GetAwaiter().GetResult();
    }

    private static string MintRsaToken(RSA rsa, string role)
    {
        var credentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);
        var handler = new JwtSecurityTokenHandler();
        handler.InboundClaimTypeMap.Clear();

        var token = handler.CreateJwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            subject: new ClaimsIdentity(new[]
            {
                new Claim("sub", Guid.NewGuid().ToString()),
                new Claim("account_id", Guid.NewGuid().ToString()),
                new Claim("role", role)
            }),
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: credentials);

        return handler.WriteToken(token);
    }

    [Fact]
    public async Task AdminIntegration_MissingToken_Returns401()
    {
        using var rsa = RSA.Create(2048);
        using var host = BuildAdminHost(rsa);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/api/v1/admin/integrations/payment");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("OWNER")]
    [InlineData("DRIVER")]
    public async Task AdminIntegration_NonAdminRole_Returns403(string role)
    {
        using var rsa = RSA.Create(2048);
        using var host = BuildAdminHost(rsa);
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", MintRsaToken(rsa, role));

        var response = await client.GetAsync("/api/v1/admin/integrations/payment");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminIntegration_AdminRole_Returns200()
    {
        using var rsa = RSA.Create(2048);
        using var host = BuildAdminHost(rsa);
        var client = host.GetTestClient();
        // UserService issues the access-token role vocabulary in lowercase
        // (AccessRoles.Map("PLATFORM_ADMIN") => "admin"), matching the controller's
        // [Authorize(Roles = "admin")].
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", MintRsaToken(rsa, "admin"));

        var response = await client.GetAsync("/api/v1/admin/integrations/payment");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"success\":true", body);
        Assert.Contains("\"mode\":\"LIVE\"", body);
    }

    // ---------------------------------------------------------------------
    // Finding 12: exception carries no HTTP status; middleware maps to 400.
    // ---------------------------------------------------------------------

    [Fact]
    public void PaymentIntegrationException_DoesNotCarryStatusCode()
    {
        Assert.Null(typeof(PaymentIntegrationException).GetProperty("StatusCode"));
    }

    // ---------------------------------------------------------------------
    // Finding 14: provider-supplied text that echoes credentials is redacted
    // before it is logged or returned.
    // ---------------------------------------------------------------------

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }

    [Fact]
    public async Task ProviderError_EchoingCredentials_IsRedactedFromLogAndReturnedMessage()
    {
        const string clientId = "live-client-echo-marker";
        const string apiKey = "api-key-echo-marker";
        const string checksumKey = "checksum-key-echo-marker";

        var store = CreateLiveStore(clientId, apiKey, checksumKey);
        var echoedBody = "{\"code\":\"01\",\"desc\":\"rejected " + clientId + " / " + apiKey +
                         " / " + checksumKey + "\",\"data\":{}}";
        var handler = new RecordingHandler(HttpStatusCode.OK, echoedBody);
        var logger = new CapturingLogger<PayOsAdapter>();
        var adapter = new PayOsAdapter(
            new HttpClient(handler),
            Options.Create(new PayOsOptions()),
            logger,
            store, CheckoutBindingFixture.Create());

        var result = await adapter.InitiatePaymentAsync(CreateRequest());

        Assert.False(result.IsSuccess);

        var emittedLog = string.Join("\n", logger.Messages);
        Assert.Contains("[REDACTED]", emittedLog);
        Assert.DoesNotContain(clientId, emittedLog);
        Assert.DoesNotContain(apiKey, emittedLog);
        Assert.DoesNotContain(checksumKey, emittedLog);

        var returnedMessage = result.ErrorMessage ?? string.Empty;
        Assert.DoesNotContain(clientId, returnedMessage);
        Assert.DoesNotContain(apiKey, returnedMessage);
        Assert.DoesNotContain(checksumKey, returnedMessage);
    }
}