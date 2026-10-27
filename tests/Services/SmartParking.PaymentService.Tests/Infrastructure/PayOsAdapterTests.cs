using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartParking.PaymentService.Domain.Ports;
using SmartParking.PaymentService.Infrastructure.Adapters.Outbound;
using SmartParking.PaymentService.Infrastructure.Options;
using SmartParking.PaymentService.Infrastructure.Services;
using Xunit;

namespace SmartParking.PaymentService.Tests.Infrastructure;

public class PayOsAdapterTests
{
    [Fact]
    public void PayOsSignatureService_ComputesAndVerifiesSignatureCorrectly()
    {
        var checksumKey = "test-checksum-key-123456";
        var dict = new Dictionary<string, object?>
        {
            ["amount"] = 25000,
            ["orderCode"] = 123456789,
            ["description"] = "Thanh toan do xe",
            ["cancelUrl"] = "https://example.com/cancel",
            ["returnUrl"] = "https://example.com/return"
        };

        var signature = PayOsSignatureService.CreateSignatureFromDictionary(dict, checksumKey);
        Assert.NotNull(signature);
        Assert.NotEmpty(signature);

        var isValid = PayOsSignatureService.VerifySignature(signature, signature);
        Assert.True(isValid);

        var isTamperedValid = PayOsSignatureService.VerifySignature(signature, "tampered-signature-value");
        Assert.False(isTamperedValid);
    }

    [Fact]
    public void PayOsSignatureService_CreateCheckoutSignature_MatchesExpectedKeysOrder()
    {
        var checksumKey = "my-secret-key";
        var sig1 = PayOsSignatureService.CreateCheckoutSignature(
            amount: 50000,
            cancelUrl: "http://cancel",
            description: "test",
            orderCode: 999,
            returnUrl: "http://return",
            checksumKey: checksumKey
        );

        var expectedData = "amount=50000&cancelUrl=http://cancel&description=test&orderCode=999&returnUrl=http://return";
        var sig2 = PayOsSignatureService.ComputeHmacSha256(expectedData, checksumKey);

        Assert.Equal(sig2, sig1);
    }

    [Fact]
    public async Task InitiatePaymentAsync_SandboxFallback_ReturnsMockUrlsAndQr()
    {
        var httpClient = new HttpClient();
        var options = Options.Create(new PayOsOptions
        {
            ClientId = "", // triggers sandbox fallback
            ApiKey = "",
            ChecksumKey = ""
        });

        var adapter = new PayOsAdapter(httpClient, options, NullLogger<PayOsAdapter>.Instance);
        var request = new PaymentInitiateRequest(
            TransactionId: Guid.NewGuid(),
            OrderCode: 20261020001,
            AmountVnd: 25000,
            Description: "Thanh toan",
            ReturnUrl: "https://example.com/return",
            CancelUrl: "https://example.com/cancel",
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(15)
        );

        var result = await adapter.InitiatePaymentAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.CheckoutUrl);
        Assert.StartsWith("https://pay.payos.vn/web/", result.CheckoutUrl);
        Assert.NotNull(result.QrCode);
        Assert.Equal("970422", result.Bin);
        Assert.Equal("SMARTPARKING CORP", result.AccountName);
    }

    [Fact]
    public async Task VerifyAndParseWebhookAsync_ValidSignature_ReturnsSuccess()
    {
        var checksumKey = "secret-key-123";
        var httpClient = new HttpClient();
        var options = Options.Create(new PayOsOptions
        {
            ChecksumKey = checksumKey
        });

        var adapter = new PayOsAdapter(httpClient, options, NullLogger<PayOsAdapter>.Instance);

        var payloadData = new Dictionary<string, object?>
        {
            ["amount"] = 25000L,
            ["orderCode"] = 123456L,
            ["reference"] = "REF9999",
            ["paymentLinkId"] = "LINK123"
        };
        var validSignature = PayOsSignatureService.CreateSignatureFromDictionary(payloadData, checksumKey);

        var webhookJson = $$"""
        {
            "code": "00",
            "desc": "success",
            "data": {
                "orderCode": 123456,
                "amount": 25000,
                "reference": "REF9999",
                "paymentLinkId": "LINK123"
            },
            "signature": "{{validSignature}}"
        }
        """;

        var result = await adapter.VerifyAndParseWebhookAsync(webhookJson, validSignature);

        Assert.True(result.IsValidSignature);
        Assert.Equal("SUCCESS", result.PaymentStatus);
        Assert.Equal(123456, result.OrderCode);
        Assert.Equal(25000, result.AmountVnd);
        Assert.Equal("REF9999", result.BankReference);
    }

    [Fact]
    public async Task VerifyAndParseWebhookAsync_TamperedSignature_ReturnsInvalidSignature()
    {
        var httpClient = new HttpClient();
        var options = Options.Create(new PayOsOptions { ChecksumKey = "secret-key" });
        var adapter = new PayOsAdapter(httpClient, options, NullLogger<PayOsAdapter>.Instance);

        var webhookJson = """
        {
            "code": "00",
            "desc": "success",
            "data": {
                "orderCode": 123456,
                "amount": 25000
            },
            "signature": "fake-invalid-signature"
        }
        """;

        var result = await adapter.VerifyAndParseWebhookAsync(webhookJson, "fake-invalid-signature");

        Assert.False(result.IsValidSignature);
        Assert.Equal("FAILED", result.PaymentStatus);
    }
}
