using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
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

    public async Task<PaymentCancellationResult> CancelPaymentAsync(
        long orderCode,
        Guid transactionId,
        string? gatewayTransactionId,
        CancellationToken cancellationToken = default)
    {
        if (IsMockTransactionId(gatewayTransactionId, transactionId))
        {
            return new PaymentCancellationResult(true, "CANCELLED", gatewayTransactionId, null, null);
        }

        if (!HasLiveCredentials())
        {
            return new PaymentCancellationResult(
                false,
                "UNKNOWN",
                gatewayTransactionId,
                "PAYOS_CREDENTIALS_NOT_CONFIGURED",
                "Live PayOS credentials are not configured; the pending checkout could not be cancelled.");
        }

        var paymentRequestId = string.IsNullOrWhiteSpace(gatewayTransactionId)
            ? orderCode.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : gatewayTransactionId;

        try
        {
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"/v2/payment-requests/{Uri.EscapeDataString(paymentRequestId)}/cancel")
            {
                Content = JsonContent.Create(new { cancellationReason = "Customer selected cash payment." })
            };
            httpRequest.Headers.Add("x-client-id", _options.ClientId);
            httpRequest.Headers.Add("x-api-key", _options.ApiKey);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            using var root = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var code = root.RootElement.TryGetProperty("code", out var codeElement) ? codeElement.GetString() : null;
            var status = root.RootElement.TryGetProperty("data", out var data) &&
                         data.TryGetProperty("status", out var statusElement)
                ? statusElement.GetString() ?? "UNKNOWN"
                : "UNKNOWN";

            if (!response.IsSuccessStatusCode || code != "00")
            {
                _logger.LogWarning(
                    "PayOS cancellation failed for OrderCode {OrderCode}: HTTP {StatusCode}, provider code {ProviderCode}.",
                    orderCode,
                    (int)response.StatusCode,
                    code);
                return new PaymentCancellationResult(
                    false,
                    status,
                    gatewayTransactionId,
                    $"PAYOS_{code ?? ((int)response.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                    "PayOS rejected the cancellation request.");
            }

            return new PaymentCancellationResult(
                status is "CANCELLED" or "PAID",
                status,
                gatewayTransactionId,
                status is "CANCELLED" or "PAID" ? null : "PAYOS_CANCELLATION_UNCONFIRMED",
                status is "CANCELLED" or "PAID" ? null : "PayOS did not confirm cancellation.");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(
                "Error cancelling PayOS checkout for OrderCode {OrderCode}: {ExceptionType}.",
                orderCode,
                ex.GetType().FullName);
            return new PaymentCancellationResult(
                false,
                "UNKNOWN",
                gatewayTransactionId,
                "PAYOS_CANCELLATION_UNKNOWN",
                "Unable to confirm PayOS checkout cancellation.");
        }
    }

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

            var mockPaymentLinkId = $"MOCK-{request.TransactionId:N}";
            return new PaymentInitiateResult(
                IsSuccess: true,
                CheckoutUrl: $"http://localhost:5173/payment/success?transactionId={request.TransactionId}",
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

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "PayOS initiate failed for OrderCode {OrderCode} with HTTP {StatusCode}.",
                    request.OrderCode,
                    (int)response.StatusCode);

                return new PaymentInitiateResult(
                    IsSuccess: false,
                    CheckoutUrl: null,
                    QrCode: null,
                    AccountNumber: null,
                    AccountName: null,
                    Bin: null,
                    GatewayTransactionId: null,
                    ErrorCode: $"PAYOS_{(int)response.StatusCode}",
                    ErrorMessage: "The payment gateway rejected the checkout request."
                );
            }

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var root = doc.RootElement;
            var code = root.TryGetProperty("code", out var codeProp) ? codeProp.GetString() : "";

            if (code != "00" || !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            {
                var safeCode = string.IsNullOrWhiteSpace(code) ? "ERROR" : code;
                _logger.LogWarning(
                    "PayOS initiate returned non-zero code {Code} for OrderCode {OrderCode}.",
                    safeCode,
                    request.OrderCode);
                return new PaymentInitiateResult(
                    IsSuccess: false,
                    CheckoutUrl: null,
                    QrCode: null,
                    AccountNumber: null,
                    AccountName: null,
                    Bin: null,
                    GatewayTransactionId: null,
                    ErrorCode: $"PAYOS_{safeCode}",
                    ErrorMessage: "The payment gateway could not create a checkout link."
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
        catch (Exception ex) when (ex is HttpRequestException || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            _logger.LogError(
                "Network failure or timeout calling PayOS for OrderCode {OrderCode}: {ExceptionType}.",
                request.OrderCode,
                ex.GetType().FullName);
            return new PaymentInitiateResult(
                IsSuccess: false,
                CheckoutUrl: null,
                QrCode: null,
                AccountNumber: null,
                AccountName: null,
                Bin: null,
                GatewayTransactionId: null,
                ErrorCode: "GATEWAY_TIMEOUT",
                ErrorMessage: "The payment gateway timed out."
            );
        }
        catch (JsonException ex)
        {
            _logger.LogError(
                "Invalid JSON response from PayOS for OrderCode {OrderCode}: {ExceptionType}.",
                request.OrderCode,
                ex.GetType().FullName);
            return new PaymentInitiateResult(
                IsSuccess: false,
                CheckoutUrl: null,
                QrCode: null,
                AccountNumber: null,
                AccountName: null,
                Bin: null,
                GatewayTransactionId: null,
                ErrorCode: "PAYOS_INVALID_RESPONSE",
                ErrorMessage: "The payment gateway returned an unexpected response."
            );
        }
    }

    public Task<PaymentCallbackResult> VerifyAndParseWebhookAsync(
        string rawPayload,
        string signature,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var doc = JsonDocument.Parse(rawPayload);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("data", out var dataEl) ||
                dataEl.ValueKind != JsonValueKind.Object)
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

            var calculatedSig = PayOsSignatureService.CreatePaymentWebhookSignature(dataEl, _options.ChecksumKey);
            var isValid = calculatedSig is not null && PayOsSignatureService.VerifySignature(calculatedSig, signature);

            long orderCode = dataEl.TryGetProperty("orderCode", out var oc) ? oc.GetInt64() : 0;
            long amount = dataEl.TryGetProperty("amount", out var am) ? am.GetInt64() : 0;
            string? reference = dataEl.TryGetProperty("reference", out var refEl) ? refEl.GetString() : null;
            string? paymentLinkId = dataEl.TryGetProperty("paymentLinkId", out var plEl) ? plEl.GetString() : null;
            string code = root.TryGetProperty("code", out var codeEl) ? codeEl.GetString() ?? "" : "";

            var status = (isValid && code == "00") ? "SUCCESS" : "FAILED";
            DateTimeOffset? transferTime = null;
            string? evidenceFailure = null;
            if (isValid)
            {
                if (!HasLiveCredentials()) evidenceFailure = "LIVE_CREDENTIALS";
                else if (ReadString(dataEl, "code") != "00") evidenceFailure = "SIGNED_SUCCESS_CODE";
                else if (ReadString(dataEl, "currency") != "VND") evidenceFailure = "CURRENCY";
                else if (DateTime.TryParseExact(ReadString(dataEl, "transactionDateTime"), "yyyy-MM-dd HH:mm:ss",
                    System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var local) &&
                    local.Ticks >= TimeSpan.FromHours(7).Ticks)
                    transferTime = new DateTimeOffset(local, TimeSpan.FromHours(7)).ToUniversalTime();
                else evidenceFailure = "TRANSFER_TIMESTAMP";
            }

            return Task.FromResult(new PaymentCallbackResult(
                IsValidSignature: isValid,
                OrderCode: orderCode,
                AmountVnd: amount,
                GatewayTransactionId: paymentLinkId,
                BankReference: reference,
                VerifiedTransferTime: transferTime,
                PaymentStatus: status,
                TransactionTime: DateTimeOffset.UtcNow,
                RawPayload: rawPayload,
                EvidenceFailure: evidenceFailure
            ));
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException)
        {
            _logger.LogError(
                "Failed to parse webhook JSON payload: {ExceptionType}.",
                ex.GetType().FullName);
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

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    public async Task<PaymentVerificationResult> QueryTransactionAsync(
        long orderCode,
        string? gatewayTransactionId,
        CancellationToken cancellationToken = default,
        string? reviewedCheckoutQr = null)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId) ||
            string.IsNullOrWhiteSpace(_options.ApiKey) ||
            _options.ClientId.StartsWith("replace-with") ||
            _options.ClientId.StartsWith("mock"))
        {
            // Sandbox fallback: an initiated checkout remains pending until the
            // explicit demo-pay flow or a webhook marks it successful.
            return new PaymentVerificationResult(
                IsFound: true,
                PaymentStatus: "PENDING",
                AmountVnd: 0,
                GatewayTransactionId: gatewayTransactionId,
                PaidAt: null
            );
        }

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"/v2/payment-requests/{orderCode}");
            httpRequest.Headers.Add("x-client-id", _options.ClientId);
            httpRequest.Headers.Add("x-api-key", _options.ApiKey);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
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

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
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
                PaidAt: normalizedStatus == "SUCCESS" ? DateTimeOffset.UtcNow : null,
                ReviewedTransfers: reviewedCheckoutQr is null ? null :
                    ReadReviewedTransfers(root, data, orderCode, gatewayTransactionId, reviewedCheckoutQr)
            );
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException ||
                                      (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            _logger.LogError(
                "Error querying PayOS for OrderCode {OrderCode}: {ExceptionType}.",
                orderCode,
                ex.GetType().FullName);
            return new PaymentVerificationResult(
                IsFound: false,
                PaymentStatus: "UNKNOWN",
                AmountVnd: 0,
                GatewayTransactionId: gatewayTransactionId,
                PaidAt: null
            );
        }
    }

    // Development status checks use the existing checkout QR as the provider's checkout-currency evidence.
    private IReadOnlyList<ReviewedPayOsTransfer>? ReadReviewedTransfers(JsonElement root, JsonElement data,
        long orderCode, string? paymentLinkId, string checkoutQr)
    {
        _logger.LogInformation("PayOS status response received for reviewed OrderCode {OrderCode}", orderCode);
        var signature = PayOsSignatureService.CreatePaymentLinkResponseSignature(data, _options.ChecksumKey);
        if (ReadString(root, "code") != "00" || signature is null ||
            !PayOsSignatureService.VerifySignature(signature, ReadString(root, "signature") ?? ""))
            return Reject("RESPONSE_SIGNATURE");

        if (!data.TryGetProperty("orderCode", out var returnedCode) || returnedCode.ValueKind != JsonValueKind.Number ||
            !returnedCode.TryGetInt64(out var returnedOrderCode) || returnedOrderCode != orderCode ||
            string.IsNullOrWhiteSpace(paymentLinkId) || ReadString(data, "id") != paymentLinkId)
            return Reject("ORDER_OR_LINK_BINDING");

        if (!PayOsCheckoutCurrency.IsVnd(checkoutQr) || ConflictingCurrency(data))
            return Reject("CHECKOUT_CURRENCY");

        if (!data.TryGetProperty("transactions", out var items) || items.ValueKind != JsonValueKind.Array)
            return Reject("TRANSFER_LIST");

        var transfers = new Dictionary<string, ReviewedPayOsTransfer>(StringComparer.Ordinal);
        foreach (var item in items.EnumerateArray())
        {
            var reference = ReadString(item, "reference");
            var time = ReadTransferTime(ReadString(item, "transactionDateTime"));
            if (ConflictingCurrency(item) || time is null || string.IsNullOrWhiteSpace(reference) || reference.Length > 255 ||
                !item.TryGetProperty("amount", out var amountField) || amountField.ValueKind != JsonValueKind.Number ||
                !amountField.TryGetInt64(out var amount) || amount <= 0)
                return Reject("TRANSFER_FIELDS");

            var transfer = new ReviewedPayOsTransfer(orderCode, paymentLinkId, reference, amount, time);
            if (!transfers.TryAdd(reference, transfer) && transfers[reference] != transfer)
                return Reject("TRANSFER_REFERENCE_CONFLICT");
        }

        _logger.LogInformation("PayOS status transfer evidence accepted for reviewed OrderCode {OrderCode}, transfer count {TransferCount}",
            orderCode, transfers.Count);
        return transfers.Values.ToArray();

        IReadOnlyList<ReviewedPayOsTransfer>? Reject(string reason)
        {
            _logger.LogWarning("PayOS status transfer evidence rejected for reviewed OrderCode {OrderCode}, reason {Reason}", orderCode, reason);
            return null;
        }
    }

    private static bool ConflictingCurrency(JsonElement data) =>
        data.TryGetProperty("currency", out var currency) &&
        (currency.ValueKind != JsonValueKind.String || currency.GetString() != "VND");

    private static DateTimeOffset? ReadTransferTime(string? value)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        var styles = System.Globalization.DateTimeStyles.None;
        if (DateTimeOffset.TryParseExact(value, ["yyyy-MM-dd'T'HH:mm:sszzz", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz"],
            culture, styles, out var offsetTime))
            return offsetTime.ToUniversalTime();
        if (DateTimeOffset.TryParseExact(value, ["yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'"],
            culture, System.Globalization.DateTimeStyles.AssumeUniversal, out var utcTime))
            return utcTime.ToUniversalTime();
        if (DateTime.TryParseExact(value, "yyyy-MM-dd HH:mm:ss", culture, styles, out var localTime) &&
            localTime.Ticks >= TimeSpan.FromHours(7).Ticks)
            return new DateTimeOffset(localTime, TimeSpan.FromHours(7)).ToUniversalTime();
        return null;
    }

    private bool HasLiveCredentials() =>
        !string.IsNullOrWhiteSpace(_options.ClientId) &&
        !string.IsNullOrWhiteSpace(_options.ApiKey) &&
        !string.IsNullOrWhiteSpace(_options.ChecksumKey) &&
        !_options.ClientId.StartsWith("replace-with", StringComparison.OrdinalIgnoreCase) &&
        !_options.ClientId.StartsWith("mock", StringComparison.OrdinalIgnoreCase);

    private static bool IsMockTransactionId(string? gatewayTransactionId, Guid transactionId) =>
        gatewayTransactionId?.StartsWith("MOCK-", StringComparison.Ordinal) == true ||
        (gatewayTransactionId is not null &&
         Guid.TryParseExact(gatewayTransactionId, "N", out var parsedId) &&
         parsedId == transactionId);
}
