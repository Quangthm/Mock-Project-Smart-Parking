using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Application.Reservations;

namespace SmartParking.OperationService.Infrastructure.Reservations;

public sealed class ReservationCommercialClient(
    HttpClient client,
    IHttpContextAccessor context,
    IConfiguration configuration) : IReservationCommercialOwner, IReservationQuoteOwner
{
    private const string Issuer = "SmartParking.OperationService";
    private const string PaymentAudience = "SmartParking.PaymentService";
    private const string DepositOrderPermission = "reservation:deposit-order";

    private const string QuotePermission = "reservation:quote";

    public Task<PaymentReservationQuote> IssuePlateQuoteAsync(ReservationQuoteBinding binding, CancellationToken ct) =>
        SendAsync<PaymentReservationQuote>(HttpMethod.Post, "/internal/reservation-quotes/v1", binding, ct,
            servicePermission: QuotePermission);

    public async Task<PaymentReservationQuote> ReadProofAsync(Guid accountId, Guid quoteId, string version, CancellationToken ct)
    {
        var proof = await SendAsync<PaymentReservationQuote>(HttpMethod.Get,
            $"/internal/reservation-quotes/v1/{quoteId:D}?accountId={accountId:D}&version={Uri.EscapeDataString(version)}",
            null, ct, servicePermission: QuotePermission);
        if (proof.QuoteId != quoteId || proof.QuoteVersion != version || proof.Binding?.AccountId != accountId ||
            proof.Binding.Coverage == null || proof.ValidUntil == default ||
            proof.Status is not ("ISSUED" or "ACCEPTED" or "EXPIRED")) throw InvalidOwner();
        return proof;
    }

    public async Task<PricePreview> PreviewAsync(Guid accountId, ReservationPricePreview intent, CancellationToken ct)
    {
        var data = await SendAsync<PreviewResponse>(
            HttpMethod.Post,
            "/api/v1/pricing/reservations/preview",
            new
            {
                siteId = intent.SiteId,
                requestedSlotId = intent.RequestedSlotId,
                start = intent.Start,
                end = intent.End,
                vehicleId = intent.VehicleId,
                vehicleType = "CAR",
                paymentMethod = intent.PaymentMethod
            },
            ct,
            accountId);
        return new(data.BookedAmount, data.DepositAmount, data.Currency,
            data.PricingVersion, data.RequiresCompleteSelection);
    }

    public async Task<DepositOrderResult> EnsureOrderAsync(DepositOrderRequest request, CancellationToken ct)
    {
        try
        {
            var data = await SendAsync<OrderResponse>(
                HttpMethod.Put,
                $"/internal/reservation-deposit-orders/{request.OperationId:D}",
                request,
                ct,
                request.AccountId,
                request.TenantId,
                request.SiteId,
                servicePermission: DepositOrderPermission);
            if (data.AcceptedRequest != request)
                return new("UNKNOWN", null, request, "PAYMENT_ORDER_RESPONSE_INVALID");
            if (data.State == "CREATED" && data.OrderId is { } orderId && orderId != Guid.Empty)
                return new("CREATED", orderId, request);
            if (data.State == "CLOSED_NO_ORDER" && data.OrderId is null)
                return new("CLOSED_NO_ORDER", null, request, "PAYMENT_ORDER_CLOSED_NO_ORDER");
            if (data.State == "UNKNOWN" && data.OrderId is null)
                return new("UNKNOWN", null, request, "PAYMENT_ORDER_OUTCOME_UNKNOWN");
            return new("UNKNOWN", null, request, "PAYMENT_ORDER_RESPONSE_INVALID");
        }
        catch (ReservationGateException error) when (IsDefinitiveOrderRejection(error))
        {
            return new("CLOSED_NO_ORDER", null, request, error.Code);
        }
        catch (ReservationGateException error) when (request.QuoteVersion.StartsWith("rq1-", StringComparison.Ordinal) &&
            error.StatusCode >= 500)
        {
            return new("UNKNOWN", null, request, "PAYMENT_ORDER_OUTCOME_UNKNOWN");
        }
    }

    public async Task<ZeroOrderDecision> AcceptZeroAsync(ZeroOrderBinding b, CancellationToken ct)
    {
        var request = new DepositOrderRequest(b.OperationId, b.ReservationId, b.AccountId, b.TenantId, b.SiteId,
            b.QuoteId, b.QuoteVersion, b.Amount, b.Currency, b.Method, b.HoldExpiresAt,
            new(b.SlotId, b.Plate, b.Start, b.End, b.IsCovered, b.CoverageVersion));
        try
        {
            var data = await SendAsync<OrderResponse>(HttpMethod.Put, $"/internal/reservation-deposit-orders/{b.OperationId:D}",
                request, ct, servicePermission: DepositOrderPermission);
            if (data.AcceptedRequest == request && data.State == "CLOSED_NO_ORDER" && data.OrderId == null && data.ReceiptId == null)
                return new("REJECTED", b, null, null, "PAYMENT_ORDER_CLOSED_NO_ORDER");
            if (data.AcceptedRequest != request || data.State != "CREATED" || data.OrderId.GetValueOrDefault() == Guid.Empty ||
                data.ReceiptId.GetValueOrDefault() == Guid.Empty) throw InvalidOwner();
            return new("ACCEPTED", b, data.OrderId, data.ReceiptId);
        }
        catch (ReservationGateException error) when (IsDefinitiveOrderRejection(error))
        {
            return new("REJECTED", b, null, null, error.Code);
        }
        catch (ReservationGateException error) when (error.StatusCode >= 500)
        {
            return new("UNKNOWN", b, null, null, "ZERO_PRICE_ACCEPTANCE_OUTCOME_UNKNOWN");
        }
    }

    public async Task ReconcileZeroAsync(ZeroOrderEvidence evidence, string reason, CancellationToken ct)
    {
        var b = evidence.Binding;
        var receipt = await SendAsync<Guid>(HttpMethod.Put, $"/internal/reservation-deposit-orders/{b.OperationId:D}/zero-disposition",
            new { b.OperationId, b.ReservationId, b.AccountId, evidence.OrderId, evidence.ReceiptId, b.QuoteId, b.QuoteVersion, reason },
            ct, servicePermission: DepositOrderPermission);
        if (receipt != evidence.ReceiptId) throw InvalidOwner();
    }

    public Task<ZeroPriceAcceptanceResult> AcceptZeroPriceAsync(ZeroPriceAcceptanceRequest request, CancellationToken ct) =>
        throw new ReservationGateException(503, "OWNER_UNAVAILABLE",
            "Zero-price reservation acceptance is not provided by PaymentService.");

    public async Task<CollectionLookup> CollectionsAsync(Guid reservationId, Guid orderId, CancellationToken ct)
    {
        var data = await SendAsync<CollectionResponse>(
            HttpMethod.Get,
            $"/internal/reservation-deposit-orders/{orderId:D}/collections",
            null,
            ct,
            servicePermission: DepositOrderPermission);
        if (data.Collections is null) throw InvalidOwner();
        var collections = new List<VerifiedCollection>(data.Collections.Count);
        foreach (var item in data.Collections)
        {
            if (item.CollectionId == Guid.Empty || item.OrderId != orderId || item.ReservationId != reservationId ||
                item.AccountId == Guid.Empty || item.TenantId == Guid.Empty || item.SiteId == Guid.Empty ||
                item.Amount <= 0 || item.Currency != "VND" || item.QuoteId == Guid.Empty ||
                string.IsNullOrWhiteSpace(item.TermsVersion)) throw InvalidOwner();
            collections.Add(new(item.CollectionId, item.OrderId, item.ReservationId, item.AccountId,
                item.TenantId, item.SiteId, item.QuoteId, item.TermsVersion, item.Amount, item.Currency));
        }
        return new(data.State, collections);
    }

    public async Task<IReadOnlyList<ReservationDepositEvidence>> ReadCollectionsAsync(Guid orderId, CancellationToken ct)
    {
        var data = await SendAsync<CollectionResponse>(HttpMethod.Get, $"/internal/reservation-deposit-orders/{orderId:D}/collections",
            null, ct, servicePermission: DepositOrderPermission);
        if (data.Collections == null) throw InvalidOwner();
        var result = new List<ReservationDepositEvidence>();
        foreach (var item in data.Collections)
        {
            var e = item.Evidence;
            if (e is not { Version: 1 } || e.EventId == Guid.Empty || e.OperationId == Guid.Empty ||
                item.OrderId != orderId || item.CollectionId == Guid.Empty || item.CollectionId != e.PaymentTransactionId ||
                item.AccountId == Guid.Empty || item.ReservationId == Guid.Empty || item.TenantId == Guid.Empty || item.SiteId == Guid.Empty ||
                item.QuoteId == Guid.Empty || string.IsNullOrWhiteSpace(item.TermsVersion) || item.Amount <= 0 || item.Currency != "VND" ||
                e.PaymentMethod != "PAYOS" || e.Provider != "PAYOS" || string.IsNullOrWhiteSpace(e.ProviderReference) ||
                e.ProviderVerifiedAt == default || e.OccurredAt == default) throw InvalidOwner();
            result.Add(new(e.EventId, item.CollectionId, orderId, e.PaymentTransactionId, e.OperationId, item.ReservationId,
                item.AccountId, item.TenantId, item.SiteId, item.QuoteId, item.TermsVersion, null, null, item.Amount,
                item.Currency, e.Provider, e.ProviderReference, e.ProviderVerifiedAt, e.OccurredAt, e.PaymentMethod));
        }
        return result;
    }

    public async Task ReconcileAsync(Guid reservationId, VerifiedCollection collection, string reason, CancellationToken ct)
    {
        if (!configuration.GetValue<bool>("ReservationIntegration:ReviewedSettlementEnabled") ||
            !collection.TermsVersion.StartsWith("rq1-", StringComparison.Ordinal)) throw Unavailable();
        var ack = await SendAsync<DispositionAcknowledgement>(HttpMethod.Post, "/internal/reservation-reconciliations",
            new { reservationId, collection.CollectionId, collection.Amount, reason }, ct, servicePermission: DepositOrderPermission);
        if (ack.ReservationId != reservationId || ack.CollectionId != collection.CollectionId || ack.RefundRequestId == Guid.Empty ||
            string.IsNullOrWhiteSpace(ack.Status)) throw InvalidOwner();
    }

    private async Task<T> SendAsync<T>(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken ct,
        Guid? accountId = null,
        Guid? tenantId = null,
        Guid? siteId = null,
        string? servicePermission = null)
    {
        using var request = CreateRequest(method, path, accountId, tenantId, siteId, servicePermission);
        if (body is not null) request.Content = JsonContent.Create(body);

        try
        {
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                // A missing versioned route or rejected service credential gives no selection evidence.
                if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden) throw Unavailable();
                if ((int)response.StatusCode < 500)
                {
                    PaymentErrorEnvelope? paymentError = null;
                    try
                    {
                        paymentError = await response.Content.ReadFromJsonAsync<PaymentErrorEnvelope>(cancellationToken: ct);
                    }
                    catch (JsonException)
                    {
                    }

                    if (servicePermission == QuotePermission && !(response.StatusCode == System.Net.HttpStatusCode.NotFound &&
                        paymentError?.ErrorCode == "QUOTE_NOT_FOUND" && method == HttpMethod.Get)) throw Unavailable();
                    throw new ReservationGateException(
                        (int)response.StatusCode,
                        paymentError?.ErrorCode ?? "PAYMENT_REQUEST_REJECTED",
                        paymentError?.Message ?? "Payment service rejected the reservation request.");
                }

                throw Unavailable();
            }

            var envelope = await response.Content.ReadFromJsonAsync<PaymentEnvelope<T>>(cancellationToken: ct);
            if (envelope is not { Success: true, Data: not null }) throw InvalidOwner();
            return envelope.Data;
        }
        catch (ReservationGateException)
        {
            throw;
        }
        catch (Exception error) when (error is HttpRequestException or JsonException ||
                                      error is OperationCanceledException && !ct.IsCancellationRequested)
        {
            throw Unavailable();
        }
    }

    private HttpRequestMessage CreateRequest(
        HttpMethod method,
        string path,
        Guid? accountId,
        Guid? tenantId,
        Guid? siteId,
        string? servicePermission)
    {
        var request = new HttpRequestMessage(method, path);
        if (servicePermission is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateServiceToken(servicePermission));
        }
        else
        {
            var authorization = context.HttpContext?.Request.Headers.Authorization.ToString();
            if (!string.IsNullOrWhiteSpace(authorization))
                request.Headers.TryAddWithoutValidation("Authorization", authorization);
        }

        if (accountId is { } account && account != Guid.Empty)
            request.Headers.TryAddWithoutValidation("X-Actor-AccountId", account.ToString("D"));
        if (tenantId is { } tenant && tenant != Guid.Empty)
            request.Headers.TryAddWithoutValidation("X-Actor-TenantId", tenant.ToString("D"));
        if (siteId is { } site && site != Guid.Empty)
            request.Headers.TryAddWithoutValidation("X-Actor-SiteId", site.ToString("D"));
        return request;
    }

    private string CreateServiceToken(string permission)
    {
        if (!configuration.GetValue<bool>("ReservationIntegration:Enabled")) throw Unavailable();
        if (client.BaseAddress is null || client.BaseAddress.Scheme != Uri.UriSchemeHttps &&
            !configuration.GetValue<bool>("ReservationIntegration:AllowHttp")) throw Unavailable();

        try
        {
            var pem = configuration["ReservationIntegration:OperationsPrivateKeyPem"];
            if (configuration["ReservationIntegration:OperationsPrivateKeyPath"] is { Length: > 0 } privatePath)
                pem = File.ReadAllText(privatePath);
            if (string.IsNullOrWhiteSpace(pem)) throw Unavailable();

            using var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            if (rsa.KeySize < 2048) throw Unavailable();

            // Each call owns and disposes its RSA key. A cached signature provider would retain it.
            var key = new RsaSecurityKey(rsa) { CryptoProviderFactory = new() { CacheSignatureProviders = false } };
            var now = DateTime.UtcNow;
            var token = new JwtSecurityToken(Issuer, PaymentAudience,
                [
                    new Claim("sub", Issuer),
                    new Claim("sender", Issuer),
                    new Claim("permission", permission),
                    new Claim("role", "INTERNAL_SERVICE"),
                    new Claim("scope", "s2s"),
                    new Claim("jti", Guid.NewGuid().ToString("N"))
                ],
                now, now.AddMinutes(1), new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        catch (Exception error) when (error is CryptographicException or IOException or UnauthorizedAccessException
                                          or ArgumentException)
        {
            throw Unavailable();
        }
    }

    private static bool IsDefinitiveOrderRejection(ReservationGateException error) =>
        (error.StatusCode is 400 or 404 or 409) &&
        error.Code != "QUOTE_BINDING_CONFLICT" &&
        (error.Code.StartsWith("QUOTE_", StringComparison.Ordinal) || error.Code == "TERMS_STALE");

    private static ReservationGateException InvalidOwner() =>
        new(503, "OWNER_CONTRACT_INVALID", "The owner response does not match the reservation contract.");

    private static ReservationGateException Unavailable() =>
        new(503, "OWNER_UNAVAILABLE", "Reservation commercial owner is unavailable.");
}
