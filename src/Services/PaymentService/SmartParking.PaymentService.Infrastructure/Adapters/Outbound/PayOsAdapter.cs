using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartParking.PaymentService.Domain.Ports;
using SmartParking.PaymentService.Infrastructure.Options;
using SmartParking.PaymentService.Infrastructure.Services;

namespace SmartParking.PaymentService.Infrastructure.Adapters.Outbound;

public class PayOsAdapter : IPaymentGatewayPort
{
    private readonly HttpClient _httpClient;
    private readonly PayOsOptions _options;
    private readonly ILogger<PayOsAdapter> _logger;

    public PayOsAdapter(
        HttpClient httpClient,
        IOptions<PayOsOptions> options,
        ILogger<PayOsAdapter> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public string ProviderCode => "PAYOS";

    public async Task<PaymentInitiateResult> InitiatePaymentAsync(
        PaymentInitiateRequest request,
        CancellationToken cancellationToken = default)
    {
        // Sandbox fallback if credentials are not configured
        if (string.IsNullOrWhiteSpace(_options.ClientId) ||
            string.IsNullOrWhiteSpace(_options.ApiKey) ||
            string.IsNullOrWhiteSpace(_options.ChecksumKey) ||
            _options.ClientId.StartsWith("replace-with") ||
            _options.ClientId.StartsWith("mock"))
        {
            _logger.LogInformation("PayOS credentials not configured or sandbox mode. Emulating payment link for OrderCode {OrderCode}.", request.OrderCode);

            var mockPaymentLinkId = Guid.NewGuid().ToString("N");
            return new PaymentInitiateResult(
                IsSuccess: true,
                CheckoutUrl: $"https://pay.payos.vn/web/{mockPaymentLinkId}",
                QrCode: $"00020101021238540010A00000072701240006970422011001234567890208QRIBFTTA5303704540{request.AmountVnd}5802VN62230819{request.Description}6304ABCD",
                AccountNumber: "0123456789",
                AccountName: "SMARTPARKING CORP",
                Bin: "970422",
                GatewayTransactionId: mockPaymentLinkId,
                ErrorCode: null,
                ErrorMessage: null
            );
        }

        try
        {
            var signature = PayOsSignatureService.CreateCheckoutSignature(
                amount: request.AmountVnd,
                cancelUrl: request.CancelUrl,
                description: request.Description,
                orderCode: request.OrderCode,
                returnUrl: request.ReturnUrl,
                checksumKey: _options.ChecksumKey
            );

            var payload = new
            {
                orderCode = request.OrderCode,
                amount = request.AmountVnd,
                description = request.Description,
                cancelUrl = request.CancelUrl,
                returnUrl = request.ReturnUrl,
                expiredAt = request.ExpiresAt.ToUnixTimeSeconds(),
                signature = signature
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v2/payment-requests")
            {
                Content = JsonContent.Create(payload)
            };

            httpRequest.Headers.Add("x-client-id", _options.ClientId);
            httpRequest.Headers.Add("x-api-key", _options.ApiKey);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("PayOS initiate failed with HTTP {StatusCode}: {ErrorBody}", response.StatusCode, errorBody);

                return new PaymentInitiateResult(
                    IsSuccess: false,
                    CheckoutUrl: null,
                    QrCode: null,
                    AccountNumber: null,
                    AccountName: null,
                    Bin: null,
                    GatewayTransactionId: null,
                    ErrorCode: $"PAYOS_{response.StatusCode}",
                    ErrorMessage: errorBody
                );
            }

            var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var root = doc.RootElement;
            var code = root.TryGetProperty("code", out var codeProp) ? codeProp.GetString() : "";
            var desc = root.TryGetProperty("desc", out var descProp) ? descProp.GetString() : "PayOS error";

            if (code != "00" || !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            {
                _logger.LogWarning("PayOS initiate returned non-zero code {Code}: {Desc}", code, desc);
                return new PaymentInitiateResult(
                    IsSuccess: false,
                    CheckoutUrl: null,
                    QrCode: null,
                    AccountNumber: null,
                    AccountName: null,
                    Bin: null,
                    GatewayTransactionId: null,
                    ErrorCode: $"PAYOS_{code}",
                    ErrorMessage: desc
                );
            }

            return new PaymentInitiateResult(
                IsSuccess: true,
                CheckoutUrl: data.TryGetProperty("checkoutUrl", out var cUrl) ? cUrl.GetString() : null,
                QrCode: data.TryGetProperty("qrCode", out var qr) ? qr.GetString() : null,
                AccountNumber: data.TryGetProperty("accountNumber", out var accNum) ? accNum.GetString() : null,
                AccountName: data.TryGetProperty("accountName", out var accName) ? accName.GetString() : null,
                Bin: data.TryGetProperty("bin", out var bin) ? bin.GetString() : null,
                GatewayTransactionId: data.TryGetProperty("paymentLinkId", out var linkId) ? linkId.GetString() : null,
                ErrorCode: null,
                ErrorMessage: null
            );
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network timeout or connection failure when calling PayOS.");
            return new PaymentInitiateResult(
                IsSuccess: false,
                CheckoutUrl: null,
                QrCode: null,
                AccountNumber: null,
                AccountName: null,
                Bin: null,
                GatewayTransactionId: null,
                ErrorCode: "GATEWAY_TIMEOUT",
                ErrorMessage: ex.Message
            );
        }
    }

    public Task<PaymentCallbackResult> VerifyAndParseWebhookAsync(
        string rawPayload,
        string signature,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawPayload);
            var root = doc.RootElement;

            if (!root.TryGetProperty("data", out var dataEl))
            {
                return Task.FromResult(new PaymentCallbackResult(
                    IsValidSignature: false,
                    OrderCode: 0,
                    AmountVnd: 0,
                    GatewayTransactionId: null,
                    BankReference: null,
                    PaymentStatus: "FAILED",
                    TransactionTime: DateTimeOffset.UtcNow,
                    RawPayload: rawPayload
                ));
            }

            var dict = new Dictionary<string, object?>();
            foreach (var prop in dataEl.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.String)
                {
                    dict[prop.Name] = prop.Value.GetString();
                }
                else if (prop.Value.ValueKind == JsonValueKind.Number)
                {
                    if (prop.Value.TryGetInt64(out var lVal))
                        dict[prop.Name] = lVal;
                    else
                        dict[prop.Name] = prop.Value.GetDouble();
                }
                else if (prop.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    dict[prop.Name] = prop.Value.GetBoolean();
                }
            }

            var calculatedSig = PayOsSignatureService.CreateSignatureFromDictionary(dict, _options.ChecksumKey);
            var isValid = PayOsSignatureService.VerifySignature(calculatedSig, signature);

            long orderCode = dataEl.TryGetProperty("orderCode", out var oc) ? oc.GetInt64() : 0;
            long amount = dataEl.TryGetProperty("amount", out var am) ? am.GetInt64() : 0;
            string? reference = dataEl.TryGetProperty("reference", out var refEl) ? refEl.GetString() : null;
            string? paymentLinkId = dataEl.TryGetProperty("paymentLinkId", out var plEl) ? plEl.GetString() : null;
            string code = root.TryGetProperty("code", out var codeEl) ? codeEl.GetString() ?? "" : "";

            var status = (isValid && code == "00") ? "SUCCESS" : "FAILED";

            return Task.FromResult(new PaymentCallbackResult(
                IsValidSignature: isValid,
                OrderCode: orderCode,
                AmountVnd: amount,
                GatewayTransactionId: paymentLinkId,
                BankReference: reference,
                PaymentStatus: status,
                TransactionTime: DateTimeOffset.UtcNow,
                RawPayload: rawPayload
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse webhook JSON payload.");
            return Task.FromResult(new PaymentCallbackResult(
                IsValidSignature: false,
                OrderCode: 0,
                AmountVnd: 0,
                GatewayTransactionId: null,
                BankReference: null,
                PaymentStatus: "FAILED",
                TransactionTime: DateTimeOffset.UtcNow,
                RawPayload: rawPayload
            ));
        }
    }

    public async Task<PaymentVerificationResult> QueryTransactionAsync(
        long orderCode,
        string? gatewayTransactionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId) ||
            string.IsNullOrWhiteSpace(_options.ApiKey) ||
            _options.ClientId.StartsWith("replace-with") ||
            _options.ClientId.StartsWith("mock"))
        {
            // Sandbox fallback
            return new PaymentVerificationResult(
                IsFound: true,
                PaymentStatus: "SUCCESS",
                AmountVnd: 25000,
                GatewayTransactionId: gatewayTransactionId ?? "MOCK-GW-TX",
                PaidAt: DateTimeOffset.UtcNow
            );
        }

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"/v2/payment-requests/{orderCode}");
            httpRequest.Headers.Add("x-client-id", _options.ClientId);
            httpRequest.Headers.Add("x-api-key", _options.ApiKey);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new PaymentVerificationResult(
                    IsFound: false,
                    PaymentStatus: "UNKNOWN",
                    AmountVnd: 0,
                    GatewayTransactionId: null,
                    PaidAt: null
                );
            }

            var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var root = doc.RootElement;
            var data = root.GetProperty("data");

            string statusStr = data.TryGetProperty("status", out var st) ? st.GetString() ?? "PENDING" : "PENDING";
            long amount = data.TryGetProperty("amount", out var am) ? am.GetInt64() : 0;

            string normalizedStatus = statusStr switch
            {
                "PAID" => "SUCCESS",
                "CANCELLED" => "CANCELLED",
                "PENDING" => "PENDING",
                _ => "UNKNOWN"
            };

            return new PaymentVerificationResult(
                IsFound: true,
                PaymentStatus: normalizedStatus,
                AmountVnd: amount,
                GatewayTransactionId: gatewayTransactionId,
                PaidAt: normalizedStatus == "SUCCESS" ? DateTimeOffset.UtcNow : null
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying PayOS for OrderCode {OrderCode}", orderCode);
            return new PaymentVerificationResult(
                IsFound: false,
                PaymentStatus: "UNKNOWN",
                AmountVnd: 0,
                GatewayTransactionId: gatewayTransactionId,
                PaidAt: null
            );
        }
    }
}
