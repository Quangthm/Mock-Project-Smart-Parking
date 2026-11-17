using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Application.Integrations;
using SmartParking.PaymentService.Infrastructure.Options;
using SmartParking.PaymentService.Infrastructure.Services;

namespace SmartParking.PaymentService.Infrastructure.Adapters.Outbound;

public class PayOsAdapter : IPaymentGatewayPort
{
    private const string DefaultBaseUrl = "https://api-merchant.payos.vn";
    private const string RedactedMarker = "[REDACTED]";

    private readonly HttpClient _httpClient;
    private readonly PayOsOptions _options;
    private readonly ILogger<PayOsAdapter> _logger;
    private readonly IPaymentIntegrationSettings? _settings;
    private readonly ICheckoutSigningBindings? _checkoutBindings;

    public PayOsAdapter(
        HttpClient httpClient,
        IOptions<PayOsOptions> options,
        ILogger<PayOsAdapter> logger,
        IPaymentIntegrationSettings? settings = null,
        ICheckoutSigningBindings? checkoutBindings = null)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _settings = settings;
        _checkoutBindings = checkoutBindings;
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

        // One immutable snapshot for the whole request: mode, base URL and credentials
        // can never be mixed across a concurrent configuration change.
        var snapshot = GetSnapshot();

        if (IsFixtureMode(snapshot) || !HasLiveCredentials(snapshot))
        {
            return new PaymentCancellationResult(
                false,
                "UNKNOWN",
                gatewayTransactionId,
                "PAYOS_CREDENTIALS_NOT_CONFIGURED",
                "Live PayOS credentials are not configured; the pending checkout could not be cancelled.");
        }

        if (!TryResolveBaseUri(snapshot.BaseUrl, out var baseUri))
        {
            _logger.LogError(
                "PayOS base URL is not a valid absolute URI; cancellation aborted for OrderCode {OrderCode}.",
                orderCode);
            return new PaymentCancellationResult(
                false,
                "UNKNOWN",
                gatewayTransactionId,
                "PAYOS_CONFIGURATION_INVALID",
                "PayOS base URL is not configured as an absolute URI.");
        }

        var paymentRequestId = string.IsNullOrWhiteSpace(gatewayTransactionId)
            ? orderCode.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : gatewayTransactionId;

        var requestUri = new Uri(baseUri, $"v2/payment-requests/{Uri.EscapeDataString(paymentRequestId)}/cancel");

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = JsonContent.Create(new { cancellationReason = "Customer selected cash payment." })
            };
            httpRequest.Headers.Add("x-client-id", snapshot.ClientId ?? string.Empty);
            httpRequest.Headers.Add("x-api-key", snapshot.ApiKey ?? string.Empty);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var root = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var code = root.RootElement.TryGetProperty("code", out var codeElement) ? codeElement.GetString() : null;
            var desc = root.RootElement.TryGetProperty("desc", out var descElement) ? descElement.GetString() : null;
            var status = root.RootElement.TryGetProperty("data", out var data) &&
                         data.TryGetProperty("status", out var statusElement)
                ? statusElement.GetString() ?? "UNKNOWN"
                : "UNKNOWN";

            if (!response.IsSuccessStatusCode || code != "00")
            {
                MarkProbe(snapshot.Version, false);
                // Redact against the in-flight snapshot as well as the current store values:
                // a key echoed by the provider during a concurrent rotation is still a secret.
                var safeCode = SanitizeProviderText(code, snapshot);
                _logger.LogWarning(
                    "PayOS cancellation failed for OrderCode {OrderCode}: HTTP {StatusCode} code {Code} desc {Description}",
                    orderCode, (int)response.StatusCode, safeCode ?? "UNKNOWN", SanitizeProviderText(desc, snapshot));
                var failureCode = safeCode
                    ?? ((int)response.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture);
                return new PaymentCancellationResult(
                    false, status, gatewayTransactionId,
                    $"PAYOS_{failureCode}",
                    $"PayOS cancellation failed (code {failureCode}).");
            }

            MarkProbe(snapshot.Version, true);
            return new PaymentCancellationResult(
                status is "CANCELLED" or "PAID",
                status,
                gatewayTransactionId,
                status is "CANCELLED" or "PAID" ? null : "PAYOS_CANCELLATION_UNCONFIRMED",
                status is "CANCELLED" or "PAID" ? null : "PayOS cancellation was not confirmed.");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            MarkProbe(snapshot.Version, false);
            _logger.LogError(ex, "Error cancelling PayOS checkout for OrderCode {OrderCode}", orderCode);
            return new PaymentCancellationResult(
                false, "UNKNOWN", gatewayTransactionId, "PAYOS_CANCELLATION_UNKNOWN",
                "The PayOS cancellation request could not be completed.");
        }
    }

    public async Task<PaymentInitiateResult> InitiatePaymentAsync(
        PaymentInitiateRequest request,
        CancellationToken cancellationToken = default)
    {
        // One immutable snapshot for the whole request.
        var snapshot = GetSnapshot();

        // FIXTURE mode always emulates locally; FIXTURE never makes outbound HTTP calls.
        if (IsFixtureMode(snapshot))
        {
            _logger.LogInformation("PayOS FIXTURE mode. Emulating payment link for OrderCode {OrderCode}.", request.OrderCode);
            return EmulatedResult(request);
        }

        if (!HasLiveCredentials(snapshot))
        {
            // LIVE mode must never silently emulate; report a configuration error and make no call.
            if (snapshot.Mode == PaymentIntegrationSettingsStore.LiveMode)
            {
                _logger.LogWarning(
                    "PayOS LIVE mode requested without configured credentials; no outbound call made for OrderCode {OrderCode}.",
                    request.OrderCode);
                return new PaymentInitiateResult(
                    IsSuccess: false,
                    CheckoutUrl: null,
                    QrCode: null,
                    AccountNumber: null,
                    AccountName: null,
                    Bin: null,
                    GatewayTransactionId: null,
                    ErrorCode: "PAYOS_CONFIGURATION_MISSING",
                    ErrorMessage: "PayOS credentials not configured for LIVE mode");
            }

            // SANDBOX emulates only while credentials remain mock/blank (zero outbound HTTP).
            _logger.LogInformation(
                "PayOS SANDBOX mode without real credentials. Emulating payment link for OrderCode {OrderCode}.",
                request.OrderCode);
            return EmulatedResult(request);
        }

        if (!TryResolveBaseUri(snapshot.BaseUrl, out var baseUri))
        {
            _logger.LogError(
                "PayOS base URL is not a valid absolute URI; initiation aborted for OrderCode {OrderCode}.",
                request.OrderCode);
            return new PaymentInitiateResult(
                IsSuccess: false,
                CheckoutUrl: null,
                QrCode: null,
                AccountNumber: null,
                AccountName: null,
                Bin: null,
                GatewayTransactionId: null,
                ErrorCode: "PAYOS_CONFIGURATION_INVALID",
                ErrorMessage: "PayOS base URL is not configured as an absolute URI.");
        }

        if (_settings is not null)
        {
            try
            {
                if (_checkoutBindings is null)
                    throw new InvalidOperationException("Missing checkout signing binding repository.");

                if (!await _checkoutBindings.BindAsync(request.OrderCode, snapshot, cancellationToken,
                    new Application.Common.Interfaces.Persistence.CheckoutBindingProvenance("CHECKOUT", PaymentOrderId: request.PaymentOrderId)))
                    return BindingFailure("CHECKOUT_BINDING_CONFLICT", "This checkout belongs to another signing configuration.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError("Could not persist the signing configuration for order {OrderCode}.", request.OrderCode);
                return BindingFailure("CHECKOUT_BINDING_UNAVAILABLE", "The checkout signing configuration could not be recorded.");
            }
        }

        try
        {
            var signature = PayOsSignatureService.CreateCheckoutSignature(
                amount: request.AmountVnd,
                cancelUrl: request.CancelUrl,
                description: request.Description,
                orderCode: request.OrderCode,
                returnUrl: request.ReturnUrl,
                checksumKey: snapshot.ChecksumKey ?? string.Empty
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

            var requestUri = new Uri(baseUri, "v2/payment-requests");
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = JsonContent.Create(payload)
            };

            httpRequest.Headers.Add("x-client-id", snapshot.ClientId ?? string.Empty);
            httpRequest.Headers.Add("x-api-key", snapshot.ApiKey ?? string.Empty);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                MarkProbe(snapshot.Version, false);
                // Never log or return the raw upstream body/headers.
                _logger.LogWarning(
                    "PayOS initiate failed with HTTP {StatusCode} for OrderCode {OrderCode}.",
                    (int)response.StatusCode, request.OrderCode);

                return new PaymentInitiateResult(
                    IsSuccess: false,
                    CheckoutUrl: null,
                    QrCode: null,
                    AccountNumber: null,
                    AccountName: null,
                    Bin: null,
                    GatewayTransactionId: null,
                    ErrorCode: $"PAYOS_{response.StatusCode}",
                    ErrorMessage: $"PayOS payment request failed (code {(int)response.StatusCode})."
                );
            }

            var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var root = doc.RootElement;
            var code = root.TryGetProperty("code", out var codeProp) ? codeProp.GetString() : "";
            var desc = root.TryGetProperty("desc", out var descProp) ? descProp.GetString() : "PayOS error";

            if (code != "00" || !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            {
                MarkProbe(snapshot.Version, false);
                // Only redacted/normalized provider metadata is logged or surfaced; raw
                // bodies are never logged and reflected credentials are scrubbed.
                var safeCode = SanitizeProviderText(code, snapshot) ?? "UNKNOWN";
                _logger.LogWarning(
                    "PayOS initiate returned non-zero code {Code} for OrderCode {OrderCode}: {Description}",
                    safeCode, request.OrderCode, SanitizeProviderText(desc, snapshot));
                return new PaymentInitiateResult(
                    IsSuccess: false,
                    CheckoutUrl: null,
                    QrCode: null,
                    AccountNumber: null,
                    AccountName: null,
                    Bin: null,
                    GatewayTransactionId: null,
                    ErrorCode: $"PAYOS_{safeCode}",
                    ErrorMessage: $"PayOS payment request failed (code {safeCode})."
                );
            }

            MarkProbe(snapshot.Version, true);

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
        catch (Exception ex) when (ex is HttpRequestException ||
            (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            MarkProbe(snapshot.Version, false);
            _logger.LogError(ex, "Network timeout or connection failure when calling PayOS for OrderCode {OrderCode}.", request.OrderCode);
            return new PaymentInitiateResult(
                IsSuccess: false,
                CheckoutUrl: null,
                QrCode: null,
                AccountNumber: null,
                AccountName: null,
                Bin: null,
                GatewayTransactionId: null,
                ErrorCode: "GATEWAY_TIMEOUT",
                ErrorMessage: "The PayOS payment request timed out or could not be completed."
            );
        }
    }

    public PaymentWebhookEnvelope ParseWebhookEnvelope(string rawPayload)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawPayload);
            var root = doc.RootElement;
            if (root.TryGetProperty("data", out var dataEl) &&
                dataEl.ValueKind == JsonValueKind.Object &&
                dataEl.TryGetProperty("orderCode", out var oc) &&
                oc.ValueKind == JsonValueKind.Number &&
                oc.TryGetInt64(out var orderCode))
            {
                return new PaymentWebhookEnvelope(orderCode);
            }
        }
        catch (JsonException)
        {
            // Unparseable payloads carry no routing facts.
        }

        return new PaymentWebhookEnvelope(0);
    }

    public async Task<PaymentCallbackResult> VerifyAndParseWebhookAsync(
        string rawPayload,
        string signature,
        DateTimeOffset? orderCreatedAt = null,
        CancellationToken cancellationToken = default)
    {
        bool lookupInProgress = false;
        try
        {
            using var doc = JsonDocument.Parse(rawPayload);
            var root = doc.RootElement;

            if (!root.TryGetProperty("data", out var dataEl))
            {
                return InvalidCallback(rawPayload);
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

            long orderCode = dataEl.TryGetProperty("orderCode", out var oc) ? oc.GetInt64() : 0;

            lookupInProgress = true;
            var trustedKey = _settings is null
                ? _options.ChecksumKey
                : _checkoutBindings is null
                    ? null
                    : await _checkoutBindings.ReadChecksumKeyAsync(orderCode, cancellationToken);
            lookupInProgress = false;
            if (string.IsNullOrWhiteSpace(trustedKey))
            {
                _logger.LogWarning(
                    "No trusted PayOS checksum key is available for the incoming webhook; rejecting the callback.");
                return InvalidCallback(rawPayload);
            }

            var isValid = PayOsSignatureService.VerifySignature(
                PayOsSignatureService.CreateSignatureFromDictionary(dict, trustedKey), signature);

            long amount = dataEl.TryGetProperty("amount", out var am) ? am.GetInt64() : 0;
            string? reference = dataEl.TryGetProperty("reference", out var refEl) ? refEl.GetString() : null;
            string? paymentLinkId = dataEl.TryGetProperty("paymentLinkId", out var plEl) ? plEl.GetString() : null;
            string code = root.TryGetProperty("code", out var codeEl) ? codeEl.GetString() ?? "" : "";

            var status = (isValid && code == "00") ? "SUCCESS" : "FAILED";

            return new PaymentCallbackResult(
                IsValidSignature: isValid,
                OrderCode: orderCode,
                AmountVnd: amount,
                GatewayTransactionId: paymentLinkId,
                BankReference: reference,
                PaymentStatus: status,
                TransactionTime: DateTimeOffset.UtcNow,
                RawPayload: rawPayload
            );
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException && !lookupInProgress)
        {
            _logger.LogError(ex, "Failed to parse webhook JSON payload.");
            return InvalidCallback(rawPayload);
        }
    }

    public async Task<PaymentVerificationResult> QueryTransactionAsync(
        long orderCode,
        string? gatewayTransactionId,
        CancellationToken cancellationToken = default)
    {
        // One immutable snapshot for the whole request.
        var snapshot = GetSnapshot();

        // FIXTURE mode and missing/mock credentials return a local PENDING result
        // without any outbound HTTP call.
        if (IsFixtureMode(snapshot) ||
            string.IsNullOrWhiteSpace(snapshot.ClientId) ||
            string.IsNullOrWhiteSpace(snapshot.ApiKey) ||
            snapshot.ClientId.StartsWith("replace-with", StringComparison.OrdinalIgnoreCase) ||
            snapshot.ClientId.StartsWith("mock", StringComparison.OrdinalIgnoreCase))
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

        if (!TryResolveBaseUri(snapshot.BaseUrl, out var baseUri))
        {
            _logger.LogError(
                "PayOS base URL is not a valid absolute URI; query aborted for OrderCode {OrderCode}.",
                orderCode);
            return new PaymentVerificationResult(
                IsFound: false,
                PaymentStatus: "UNKNOWN",
                AmountVnd: 0,
                GatewayTransactionId: gatewayTransactionId,
                PaidAt: null
            );
        }

        var requestUri = new Uri(baseUri, $"v2/payment-requests/{orderCode}");

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, requestUri);
            httpRequest.Headers.Add("x-client-id", snapshot.ClientId ?? string.Empty);
            httpRequest.Headers.Add("x-api-key", snapshot.ApiKey ?? string.Empty);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                MarkProbe(snapshot.Version, false);
                _logger.LogWarning(
                    "PayOS query failed with HTTP {StatusCode} for OrderCode {OrderCode}.",
                    (int)response.StatusCode, orderCode);
                return new PaymentVerificationResult(
                    IsFound: false,
                    PaymentStatus: "UNKNOWN",
                    AmountVnd: 0,
                    GatewayTransactionId: null,
                    PaidAt: null
                );
            }

            MarkProbe(snapshot.Version, true);
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
            MarkProbe(snapshot.Version, false);
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

    private PaymentConfigurationSnapshot GetSnapshot()
    {
        if (_settings is not null)
        {
            return _settings.GetCurrentSnapshot();
        }

        var mode = NormalizeMode(_options.Mode);
        var baseUrl = string.IsNullOrWhiteSpace(_options.BaseUrl) ? DefaultBaseUrl : _options.BaseUrl;
        return new PaymentConfigurationSnapshot(
            Version: 0,
            Mode: mode,
            BaseUrl: baseUrl,
            ClientId: _options.ClientId,
            ApiKey: _options.ApiKey,
            ChecksumKey: _options.ChecksumKey);
    }

    private static PaymentInitiateResult BindingFailure(string code, string message) =>
        new(false, null, null, null, null, null, null, code, message);

    private static bool IsFixtureMode(PaymentConfigurationSnapshot snapshot) =>
        snapshot.Mode == PaymentIntegrationSettingsStore.FixtureMode;

    private static string NormalizeMode(string? mode)
    {
        var normalized = mode?.Trim().ToUpperInvariant();
        return normalized switch
        {
            PaymentIntegrationSettingsStore.LiveMode => PaymentIntegrationSettingsStore.LiveMode,
            PaymentIntegrationSettingsStore.FixtureMode => PaymentIntegrationSettingsStore.FixtureMode,
            _ => PaymentIntegrationSettingsStore.SandboxMode,
        };
    }

    private static PaymentCallbackResult InvalidCallback(string rawPayload) =>
        new(
            IsValidSignature: false,
            OrderCode: 0,
            AmountVnd: 0,
            GatewayTransactionId: null,
            BankReference: null,
            PaymentStatus: "FAILED",
            TransactionTime: DateTimeOffset.UtcNow,
            RawPayload: rawPayload
        );

    private static PaymentInitiateResult EmulatedResult(PaymentInitiateRequest request) =>
        new(
            IsSuccess: true,
            CheckoutUrl: $"http://localhost:5173/payment/success?transactionId={request.TransactionId}",
            QrCode: $"00020101021238540010A00000072701240006970422011001234567890208QRIBFTTA5303704540{request.AmountVnd}5802VN62230819{request.Description}6304ABCD",
            AccountNumber: "0123456789",
            AccountName: "SMARTPARKING CORP",
            Bin: "970422",
            GatewayTransactionId: $"MOCK-{request.TransactionId:N}",
            ErrorCode: null,
            ErrorMessage: null);

    /// <summary>
    /// Builds the outbound base URI from the Admin-configured base URL. The trailing
    /// slash preserves any configured path prefix; the relative request path has no
    /// leading slash so a prefix is not discarded.
    /// </summary>
    private static bool TryResolveBaseUri(string rawBaseUrl, out Uri baseUri)
    {
        var raw = rawBaseUrl ?? string.Empty;
        if (Uri.TryCreate(raw.TrimEnd('/') + "/", UriKind.Absolute, out var parsed) &&
            (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps))
        {
            baseUri = parsed;
            return true;
        }

        baseUri = null!;
        return false;
    }

    private void MarkProbe(int configVersion, bool success)
    {
        // Version-gated: a probe from a superseded configuration cannot overwrite the
        // health of the configuration that replaced it.
        _settings?.MarkProbeResult(configVersion, success);
    }

    /// <summary>
    /// Guards against logging or surfacing provider-supplied text: every known credential
    /// value (current and retained) is redacted, then line breaks are collapsed and the
    /// result is truncated to a bounded length.
    /// </summary>
    private string? SanitizeProviderText(string? value, PaymentConfigurationSnapshot? snapshot = null, int maxLength = 200)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var redacted = value;
        var secrets = new HashSet<string>(EnumerateSensitiveValues(), StringComparer.Ordinal);
        if (snapshot is not null)
        {
            if (!string.IsNullOrWhiteSpace(snapshot.ClientId)) secrets.Add(snapshot.ClientId);
            if (!string.IsNullOrWhiteSpace(snapshot.ApiKey)) secrets.Add(snapshot.ApiKey);
            if (!string.IsNullOrWhiteSpace(snapshot.ChecksumKey)) secrets.Add(snapshot.ChecksumKey);
        }

        foreach (var secret in secrets)
        {
            if (!string.IsNullOrEmpty(secret))
            {
                redacted = redacted.Replace(secret, RedactedMarker, StringComparison.Ordinal);
            }
        }

        var normalized = redacted.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private IEnumerable<string> EnumerateSensitiveValues()
    {
        if (_settings is not null)
        {
            return _settings.GetSensitiveValues();
        }

        return new[] { _options.ClientId, _options.ApiKey, _options.ChecksumKey, _options.PreviousChecksumKey }
            .Where(secret => !string.IsNullOrWhiteSpace(secret))
            .Select(secret => secret!);
    }

    private static bool HasLiveCredentials(PaymentConfigurationSnapshot snapshot) =>
        !string.IsNullOrWhiteSpace(snapshot.ClientId) &&
        !string.IsNullOrWhiteSpace(snapshot.ApiKey) &&
        !string.IsNullOrWhiteSpace(snapshot.ChecksumKey) &&
        !snapshot.ClientId.StartsWith("replace-with", StringComparison.OrdinalIgnoreCase) &&
        !snapshot.ClientId.StartsWith("mock", StringComparison.OrdinalIgnoreCase);

    private static bool IsMockTransactionId(string? gatewayTransactionId, Guid transactionId) =>
        gatewayTransactionId?.StartsWith("MOCK-", StringComparison.Ordinal) == true ||
        (gatewayTransactionId is not null &&
         Guid.TryParseExact(gatewayTransactionId, "N", out var parsedId) &&
         parsedId == transactionId);
}
