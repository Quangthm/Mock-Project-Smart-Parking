using System.Net;
using Microsoft.AspNetCore.DataProtection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Infrastructure.Adapters.Outbound;
using SmartParking.PaymentService.Infrastructure.Options;
using SmartParking.PaymentService.Infrastructure.Services;
using Xunit;

namespace SmartParking.PaymentService.Tests;

public class PayOsWebhookSecurityAndRotationTests
{
    private static (string WebhookJson, string Signature) BuildWebhookPayload(
        long orderCode,
        long amount,
        string checksumKey,
        string code = "00")
    {
        var data = new Dictionary<string, object?>
        {
            ["amount"] = amount,
            ["orderCode"] = orderCode,
            ["reference"] = "REF_" + orderCode,
            ["paymentLinkId"] = "LINK_" + orderCode
        };

        var signature = PayOsSignatureService.CreateSignatureFromDictionary(data, checksumKey);

        var json = $$"""
        {
            "code": "{{code}}",
            "desc": "success",
            "data": {
                "orderCode": {{orderCode}},
                "amount": {{amount}},
                "reference": "REF_{{orderCode}}",
                "paymentLinkId": "LINK_{{orderCode}}"
            },
            "signature": "{{signature}}"
        }
        """;

        return (json, signature);
    }

    [Fact]
    public async Task Webhook_OrderCreatedInSandbox_ThenModeSwitchedToLive_VerifiesSuccessfully()
    {
        const string sandboxKey = "sandbox-checksum-secret-key-123456";
        const string liveKey = "live-checksum-secret-key-987654";

        var options = Options.Create(new PayOsOptions
        {
            Mode = "SANDBOX",
            LiveChecksumKey = liveKey,
            SandboxChecksumKey = sandboxKey,
            SandboxClientId = "sandbox-client",
            SandboxApiKey = "sandbox-api"
        });

        var store = new PaymentIntegrationSettingsStore(options);
        var orderCreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5);

        var repository = new CheckoutBindingFixture();
        var protection = new EphemeralDataProtectionProvider();
        var issued = new CheckoutSigningBindings(repository, protection);
        Assert.True(await issued.BindAsync(123456L, store.GetCurrentSnapshot(), default));

        // 2. Admin switches mode to LIVE with live credentials.
        store.Save(
            mode: "LIVE",
            baseUrl: "https://api-merchant.payos.vn",
            clientId: "live-client",
            apiKey: "live-api",
            checksumKey: liveKey);

        Assert.Equal("LIVE", store.GetCurrent().Mode);

        // 3. Webhook callback arrives for the old order, signed with sandbox key.
        var (webhookJson, signature) = BuildWebhookPayload(123456L, 50000L, sandboxKey);

        var adapter = new PayOsAdapter(new HttpClient(), options, new TestLogger<PayOsAdapter>(), store, new CheckoutSigningBindings(repository, protection));

        var callbackResult = await adapter.VerifyAndParseWebhookAsync(webhookJson, signature, orderCreatedAt);

        Assert.True(callbackResult.IsValidSignature);
        Assert.Equal("SUCCESS", callbackResult.PaymentStatus);
        Assert.Equal(123456L, callbackResult.OrderCode);
        Assert.Equal(50000L, callbackResult.AmountVnd);
    }

    [Fact]
    public async Task Webhook_ConsecutiveRotations_AcceptsBoundOrderAndRejectsUnboundOrder()
    {
        const string key0 = "checksum-key-version-0-initial";
        const string key1 = "checksum-key-version-1-rotated";
        const string key2 = "checksum-key-version-2-latest";

        var options = Options.Create(new PayOsOptions
        {
            Mode = "SANDBOX",
            SandboxChecksumKey = key0,
            SandboxClientId = "client",
            SandboxApiKey = "api"
        });

        var store = new PaymentIntegrationSettingsStore(options);

        var bindings = CheckoutBindingFixture.Create();
        Assert.True(await bindings.BindAsync(777888L, store.GetCurrentSnapshot(), default));
        store.Save("SANDBOX", "https://api-merchant.payos.vn", null, null, key1);
        store.Save("SANDBOX", "https://api-merchant.payos.vn", null, null, key2);

        var adapter = new PayOsAdapter(new HttpClient(), options, new TestLogger<PayOsAdapter>(), store, bindings);

        var (webhookJson, signature) = BuildWebhookPayload(777888L, 30000L, key0);
        var resultInGrace = await adapter.VerifyAndParseWebhookAsync(webhookJson, signature);

        Assert.True(resultInGrace.IsValidSignature);
        Assert.Equal("SUCCESS", resultInGrace.PaymentStatus);

        // A valid signature for the active mode cannot authorize an order with no recorded binding.
        var (webhookOldJson, signatureOld) = BuildWebhookPayload(999000L, 30000L, key2);
        var resultExpired = await adapter.VerifyAndParseWebhookAsync(webhookOldJson, signatureOld, DateTimeOffset.UtcNow);

        Assert.False(resultExpired.IsValidSignature);
        Assert.Equal("FAILED", resultExpired.PaymentStatus);
    }

    [Fact]
    public async Task Webhook_NoChecksumKeyConfigured_FailsClosedImmediately()
    {
        var options = Options.Create(new PayOsOptions
        {
            Mode = "FIXTURE",
            ChecksumKey = string.Empty,
            SandboxChecksumKey = string.Empty,
            LiveChecksumKey = string.Empty
        });

        var store = new PaymentIntegrationSettingsStore(options);
        var adapter = new PayOsAdapter(new HttpClient(), options, new TestLogger<PayOsAdapter>(), store);

        var (webhookJson, signature) = BuildWebhookPayload(111222L, 10000L, "arbitrary-key");

        var result = await adapter.VerifyAndParseWebhookAsync(webhookJson, signature);

        Assert.False(result.IsValidSignature);
        Assert.Equal("FAILED", result.PaymentStatus);
    }

    [Fact]
    public async Task LogSanitization_ProviderErrorEchoesCredential_RedactsCredentialMarkers()
    {
        const string secretApiKey = "secret_live_api_key_xyz_789";
        const string secretClientId = "secret_live_client_id_abc_123";
        const string secretChecksumKey = "secret_live_checksum_key_456";

        var options = Options.Create(new PayOsOptions
        {
            Mode = "LIVE",
            LiveClientId = secretClientId,
            LiveApiKey = secretApiKey,
            LiveChecksumKey = secretChecksumKey,
            BaseUrl = "https://api-merchant.payos.vn"
        });

        var store = new PaymentIntegrationSettingsStore(options);
        var logger = new TestLogger<PayOsAdapter>();

        // Custom HttpMessageHandler returning a provider error that echoes credentials
        var errorResponseJson = $$"""
        {
            "code": "401",
            "desc": "Authentication failed for client {{secretClientId}} with api key {{secretApiKey}}",
            "data": null
        }
        """;

        var handler = new MockHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(errorResponseJson, System.Text.Encoding.UTF8, "application/json")
        });

        var httpClient = new HttpClient(handler);
        var adapter = new PayOsAdapter(httpClient, options, logger, store, CheckoutBindingFixture.Create());

        var request = new PaymentInitiateRequest(
            TransactionId: Guid.NewGuid(),
            OrderCode: 987654321,
            AmountVnd: 50000,
            Description: "Parking fee",
            ReturnUrl: "https://example.com/return",
            CancelUrl: "https://example.com/cancel",
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(15)
        );

        var result = await adapter.InitiatePaymentAsync(request);

        Assert.False(result.IsSuccess);
        Assert.DoesNotContain(secretApiKey, result.ErrorMessage);
        Assert.DoesNotContain(secretClientId, result.ErrorMessage);
        Assert.DoesNotContain(secretChecksumKey, result.ErrorMessage);

        // Check logger outputs
        Assert.NotEmpty(logger.Logs);
        foreach (var log in logger.Logs)
        {
            Assert.DoesNotContain(secretApiKey, log.Message);
            Assert.DoesNotContain(secretClientId, log.Message);
            Assert.DoesNotContain(secretChecksumKey, log.Message);
            if (log.Message.Contains("PayOS initiate returned non-zero code"))
            {
                Assert.Contains("[REDACTED]", log.Message);
            }
        }
    }

    private sealed class MockHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response);
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<(LogLevel LogLevel, string Message, Exception? Exception)> Logs { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Logs.Add((logLevel, formatter(state, exception), exception));
        }
    }
}
