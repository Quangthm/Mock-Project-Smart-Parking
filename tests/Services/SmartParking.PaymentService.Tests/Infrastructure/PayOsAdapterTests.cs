using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Infrastructure.Adapters.Outbound;
using SmartParking.PaymentService.Infrastructure.Options;
using SmartParking.PaymentService.Infrastructure.Services;
using Xunit;

namespace SmartParking.PaymentService.Tests.Infrastructure;

public class PayOsAdapterTests
{
    private const string VndCheckoutQr =
        "00020101021238570010A00000072701270006970422011300110037630580208QRIBFTTA53037045405100005802VN62120808DH00000163047616";

    [Theory]
    [InlineData("valid", true)]
    [InlineData("tampered", false)]
    [InlineData("link", false)]
    [InlineData("order", false)]
    [InlineData("currency", false)]
    [InlineData("transfer-currency", false)]
    [InlineData("qr-currency", false)]
    [InlineData("missing-reference", false)]
    [InlineData("missing-time", false)]
    [InlineData("duplicate-reference-conflict", false)]
    [InlineData("aggregate-only", false)]
    public async Task QueryTransactionAsync_RequiresSignedBoundTransferAndCheckoutCurrency(string scenario, bool accepted)
    {
        const string checksum = "fixture-checksum";
        const string data = """
            {"status":"PAID","orderCode":123456,"id":"LINK-123","canceledAt":null,"transactions":[
              {"reference":"BANK-1","description":"Đã trả","transactionDateTime":"2026-11-26T17:49:03+07:00","amount":25000}]}
            """;
        const string canonical = """canceledAt=&id=LINK-123&orderCode=123456&status=PAID&transactions=[{"amount":25000,"description":"Đã trả","reference":"BANK-1","transactionDateTime":"2026-11-26T17:49:03+07:00"}]""";
        var responseData = scenario switch
        {
            "link" => data.Replace("LINK-123", "LINK-456"),
            "order" => data.Replace("123456", "234567"),
            "currency" => data.Insert(1, "\"currency\":\"USD\","),
            "transfer-currency" => data.Replace("{\"reference\"", "{\"currency\":\"USD\",\"reference\""),
            "missing-reference" => data.Replace("BANK-1", ""),
            "missing-time" => data.Replace("2026-11-26T17:49:03+07:00", ""),
            "duplicate-reference-conflict" => data.Replace("25000}]", """
                25000},{"reference":"BANK-1","transactionDateTime":"2026-11-26T17:49:03+07:00","amount":25001}]
                """),
            "aggregate-only" => """{"status":"PAID","orderCode":123456,"id":"LINK-123","amountPaid":25000}""",
            _ => data
        };
        using var body = System.Text.Json.JsonDocument.Parse(responseData);
        var signature = responseData == data ? PayOsSignatureService.ComputeHmacSha256(canonical, checksum) :
            PayOsSignatureService.CreatePaymentLinkResponseSignature(body.RootElement, checksum);
        if (scenario == "tampered")
            responseData = responseData.Replace("25000", "25001");

        using var http = new HttpClient(new StatusResponseHandler(responseData, signature))
        {
            BaseAddress = new Uri("https://api-merchant.payos.vn")
        };
        var adapter = new PayOsAdapter(http, Options.Create(new PayOsOptions
        {
            ClientId = "test-client", ApiKey = "test-api", ChecksumKey = checksum
        }), NullLogger<PayOsAdapter>.Instance);

        var qr = scenario == "qr-currency" ? VndCheckoutQr.Replace("5303704", "5303840") : VndCheckoutQr;
        var checkedStatus = await adapter.QueryTransactionAsync(123456, "LINK-123", reviewedCheckoutQr: qr);
        Assert.Equal("SUCCESS", checkedStatus.PaymentStatus);
        if (accepted)
        {
            var transfer = Assert.Single(checkedStatus.ReviewedTransfers!);
            Assert.Equal(new ReviewedPayOsTransfer(123456, "LINK-123", "BANK-1", 25000,
                new DateTimeOffset(2026, 11, 26, 10, 49, 03, TimeSpan.Zero)), transfer);
        }
        else
            Assert.Null(checkedStatus.ReviewedTransfers);

        var legacy = await adapter.QueryTransactionAsync(123456, "LINK-123");
        Assert.Equal("SUCCESS", legacy.PaymentStatus);
        Assert.Null(legacy.ReviewedTransfers);
    }

    private sealed class StatusResponseHandler(string data, string? signature) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/v2/payment-requests/123456", request.RequestUri?.AbsolutePath);
            Assert.Equal("test-client", Assert.Single(request.Headers.GetValues("x-client-id")));
            Assert.Equal("test-api", Assert.Single(request.Headers.GetValues("x-api-key")));
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent($$"""{"code":"00","data":{{data}},"signature":"{{signature}}"}""")
            });
        }
    }

    [Fact]
    public async Task VerifyAndParseWebhookAsync_AcceptsPublishedPayOsSignature()
    {
        // Public payOS payment-request HMAC example, independent of this implementation.
        const string checksum = "1a54716c8f0efb2744fb28b6e38b25da7f67a925d98bc1c18bd8faaecadd7675";
        const string signature = "412e915d2871504ed31be63c8f62a149a4410d34c4c42affc9006ef9917eaa03";
        const string payload = """
            {"code":"00","data":{"orderCode":123,"amount":3000,"description":"VQRIO123",
             "accountNumber":"12345678","reference":"TF230204212323","transactionDateTime":"2023-02-04 18:25:00",
             "currency":"VND","paymentLinkId":"124c33293c43417ab7879e14c8d9eb18","code":"00","desc":"Thành công",
             "counterAccountBankId":"","counterAccountBankName":"","counterAccountName":"","counterAccountNumber":"",
             "virtualAccountName":"","virtualAccountNumber":""}}
            """;
        using var http = new HttpClient();
        var adapter = new PayOsAdapter(http, Options.Create(new PayOsOptions
        {
            ClientId = "test-client", ApiKey = "test-api", ChecksumKey = checksum
        }), NullLogger<PayOsAdapter>.Instance);

        var verified = await adapter.VerifyAndParseWebhookAsync(payload, signature);

        Assert.True(verified.IsValidSignature);
        Assert.Equal(new DateTimeOffset(2023, 2, 4, 11, 25, 0, TimeSpan.Zero), verified.VerifiedTransferTime);
        Assert.False((await adapter.VerifyAndParseWebhookAsync(payload.Replace("3000", "3001"), signature)).IsValidSignature);
    }

    [Fact]
    public void PaymentWebhookSignature_FormatsSupportedScalarsAndRejectsAmbiguousData()
    {
        using var data = System.Text.Json.JsonDocument.Parse("""{"n":123,"z":null,"b":true,"a":"","q":"undefined"}""");
        var expected = PayOsSignatureService.ComputeHmacSha256("a=&b=true&n=123&q=&z=", "fixture");
        Assert.Equal(expected, PayOsSignatureService.CreatePaymentWebhookSignature(data.RootElement, "fixture"));

        using var structured = System.Text.Json.JsonDocument.Parse("""{"nested":{"a":1}}""");
        using var duplicate = System.Text.Json.JsonDocument.Parse("""{"n":1,"n":2}""");
        Assert.Null(PayOsSignatureService.CreatePaymentWebhookSignature(structured.RootElement, "fixture"));
        Assert.Null(PayOsSignatureService.CreatePaymentWebhookSignature(duplicate.RootElement, "fixture"));
    }

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
        Assert.StartsWith("http://localhost:5173/payment/success?transactionId=", result.CheckoutUrl);
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
