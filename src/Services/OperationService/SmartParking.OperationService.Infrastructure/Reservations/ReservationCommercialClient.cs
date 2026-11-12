using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Application.Reservations;

namespace SmartParking.OperationService.Infrastructure.Reservations;

public sealed class ReservationCommercialClient(
    HttpClient client,
    IHttpContextAccessor context,
    IConfiguration configuration) : IReservationCommercialOwner
{
    public async Task<PricePreview> PreviewAsync(Guid accountId, ReservationPricePreview intent, CancellationToken ct)
    {
        var data = await SendAsync<PaymentEnvelope<PreviewResponse>>(
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
        return new(data.Data.BookedAmount, data.Data.DepositAmount, data.Data.Currency,
            data.Data.PricingVersion, data.Data.RequiresCompleteSelection);
    }

    public async Task<CommercialTerms> QuoteAsync(Guid accountId, Guid tenantId, BookingSelection intent, CancellationToken ct)
    {
        var data = await SendAsync<PaymentEnvelope<QuoteResponse>>(
            HttpMethod.Post,
            "/api/v1/pricing/reservations/quote",
            new
            {
                siteId = intent.SiteId,
                requestedSlotId = intent.RequestedSlotId,
                start = intent.Start,
                end = intent.End,
                vehicleId = intent.VehicleId,
                paymentMethod = intent.PaymentMethod,
                accountId,
                tenantId = tenantId == Guid.Empty ? (Guid?)null : tenantId,
                vehicleType = "CAR",
                prepaymentMode = "FULL",
                coverageVersion = "STANDARD_v1"
            },
            ct,
            accountId,
            tenantId,
            intent.SiteId);
        if (data.Data.Selection is null) throw InvalidOwner();
        var selection = new BookingSelection(data.Data.Selection.VehicleId, data.Data.Selection.SiteId,
            data.Data.Selection.RequestedSlotId, data.Data.Selection.Start, data.Data.Selection.End,
            data.Data.Selection.PaymentMethod);
        return new(data.Data.QuoteId, data.Data.Version, data.Data.ValidUntil, selection, data.Data.AccountId,
            data.Data.BookedAmount, data.Data.DepositAmount, data.Data.Currency, data.Data.PrepaymentMode,
            data.Data.CoverageVersion, data.Data.Disclosure);
    }

    public async Task<DepositOrderResult> EnsureOrderAsync(DepositOrderRequest request, CancellationToken ct)
    {
        var accepted = request.AcceptedTerms;
        var payload = new
        {
            operationId = request.OperationId,
            reservationId = request.ReservationId,
            accountId = request.AccountId,
            tenantId = request.TenantId,
            siteId = request.SiteId,
            quoteId = accepted.QuoteId,
            termsVersion = accepted.Version,
            amount = accepted.DepositAmount,
            currency = accepted.Currency,
            paymentMethod = accepted.Selection.PaymentMethod,
            expiresAt = request.ExpiresAt
        };

        try
        {
            var data = await SendAsync<PaymentEnvelope<OrderResponse>>(
                HttpMethod.Put,
                $"/internal/reservation-deposit-orders/{request.OperationId:D}",
                payload,
                ct,
                request.AccountId,
                request.TenantId,
                request.SiteId);
            if (!Matches(data.Data.AcceptedRequest, request))
                return new("UNKNOWN", null, request, "PAYMENT_ORDER_RESPONSE_INVALID");
            if (data.Data.State == "CREATED" && data.Data.OrderId is { } orderId && orderId != Guid.Empty)
                return new("CREATED", orderId, request);
            if (data.Data.State == "CLOSED_NO_ORDER" && data.Data.OrderId is null)
                return new("CLOSED_NO_ORDER", null, request, "PAYMENT_ORDER_CLOSED_NO_ORDER");
            if (data.Data.State == "UNKNOWN" && data.Data.OrderId is null)
                return new("UNKNOWN", null, request, "PAYMENT_ORDER_OUTCOME_UNKNOWN");
            return new("UNKNOWN", null, request, "PAYMENT_ORDER_RESPONSE_INVALID");
        }
        catch (ReservationGateException error) when (IsDefinitiveOrderRejection(error))
        {
            return new("CLOSED_NO_ORDER", null, request, error.Code);
        }
    }

    public Task<ZeroPriceAcceptanceResult> AcceptZeroPriceAsync(ZeroPriceAcceptanceRequest request, CancellationToken ct) =>
        throw new ReservationGateException(503, "OWNER_UNAVAILABLE",
            "Zero-price reservation acceptance is not provided by PaymentService.");

    public async Task<CollectionLookup> CollectionsAsync(Guid reservationId, Guid orderId, CancellationToken ct)
    {
        var data = await SendAsync<PaymentEnvelope<CollectionResponse>>(
            HttpMethod.Get,
            $"/internal/reservation-deposit-orders/{orderId:D}/collections",
            null,
            ct);
        if (data.Data.Collections is null) throw InvalidOwner();
        var collections = new List<VerifiedCollection>(data.Data.Collections.Count);
        foreach (var item in data.Data.Collections)
        {
            if (item.CollectionId == Guid.Empty || item.OrderId != orderId || item.ReservationId != reservationId ||
                item.AccountId == Guid.Empty || item.TenantId == Guid.Empty || item.SiteId == Guid.Empty ||
                item.Amount <= 0 || item.Currency != "VND" || item.QuoteId == Guid.Empty ||
                string.IsNullOrWhiteSpace(item.TermsVersion)) throw InvalidOwner();
            collections.Add(new(item.CollectionId, item.OrderId, item.ReservationId, item.AccountId,
                item.TenantId, item.SiteId, item.QuoteId, item.TermsVersion, item.Amount, item.Currency));
        }
        return new(data.Data.State, collections);
    }

    public Task ReconcileAsync(Guid reservationId, VerifiedCollection collection, string reason, CancellationToken ct) =>
        throw new ReservationGateException(503, "OWNER_UNAVAILABLE",
            "Reservation reconciliation is not enabled.");

    private async Task<T> SendAsync<T>(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken ct,
        Guid? accountId = null,
        Guid? tenantId = null,
        Guid? siteId = null)
    {
        using var request = CreateRequest(method, path, accountId, tenantId, siteId);
        if (body is not null) request.Content = JsonContent.Create(body);

        try
        {
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
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

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, Guid? accountId, Guid? tenantId, Guid? siteId)
    {
        var request = new HttpRequestMessage(method, path);
        var serviceKey = configuration["Services:Key"];
        if (!string.IsNullOrWhiteSpace(serviceKey))
            request.Headers.TryAddWithoutValidation("X-Internal-Service-Key", serviceKey);

        var authorization = context.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization))
            request.Headers.TryAddWithoutValidation("Authorization", authorization);

        if (accountId is { } account && account != Guid.Empty)
            request.Headers.TryAddWithoutValidation("X-Actor-AccountId", account.ToString("D"));
        if (tenantId is { } tenant && tenant != Guid.Empty)
            request.Headers.TryAddWithoutValidation("X-Actor-TenantId", tenant.ToString("D"));
        if (siteId is { } site && site != Guid.Empty)
            request.Headers.TryAddWithoutValidation("X-Actor-SiteId", site.ToString("D"));
        return request;
    }

    private static bool Matches(AcceptedRequest? echoed, DepositOrderRequest request)
    {
        var accepted = request.AcceptedTerms;
        return echoed is not null &&
               echoed.OperationId == request.OperationId &&
               echoed.ReservationId == request.ReservationId &&
               echoed.AccountId == request.AccountId &&
               echoed.TenantId == request.TenantId &&
               echoed.SiteId == request.SiteId &&
               echoed.QuoteId == accepted.QuoteId &&
               echoed.TermsVersion == accepted.Version &&
               echoed.Amount == accepted.DepositAmount &&
               echoed.Currency == accepted.Currency &&
               echoed.PaymentMethod == accepted.Selection.PaymentMethod &&
               echoed.ExpiresAt == request.ExpiresAt;
    }

    private static bool IsDefinitiveOrderRejection(ReservationGateException error) =>
        (error.StatusCode is 400 or 404 or 409) &&
        (error.Code.StartsWith("QUOTE_", StringComparison.Ordinal) || error.Code == "TERMS_STALE");

    private static ReservationGateException InvalidOwner() =>
        new(503, "OWNER_CONTRACT_INVALID", "The owner response does not match the reservation contract.");

    private static ReservationGateException Unavailable() =>
        new(503, "OWNER_UNAVAILABLE", "Reservation commercial owner is unavailable.");

    private sealed record PaymentEnvelope<T>(bool Success, T Data);
    private sealed record PaymentErrorEnvelope(string? ErrorCode, string? Message);
    private sealed record PreviewResponse(decimal BookedAmount, decimal? DepositAmount, string Currency,
        string PricingVersion, bool RequiresCompleteSelection);
    private sealed record QuoteSelection(Guid VehicleId, Guid SiteId, Guid RequestedSlotId,
        DateTimeOffset Start, DateTimeOffset End, string PaymentMethod);
    private sealed record QuoteResponse(Guid QuoteId, string Version, DateTimeOffset ValidUntil,
        QuoteSelection Selection, Guid AccountId, decimal BookedAmount, decimal DepositAmount, string Currency,
        string PrepaymentMode, string CoverageVersion, string Disclosure);
    private sealed record AcceptedRequest(Guid OperationId, Guid ReservationId, Guid AccountId, Guid TenantId,
        Guid SiteId, Guid QuoteId, string TermsVersion, decimal Amount, string Currency, string PaymentMethod,
        DateTimeOffset ExpiresAt);
    private sealed record OrderResponse(string State, Guid? OrderId, AcceptedRequest? AcceptedRequest);
    private sealed record CollectionItem(Guid CollectionId, Guid OrderId, Guid ReservationId, Guid AccountId,
        Guid TenantId, Guid SiteId, Guid QuoteId, string TermsVersion, decimal Amount, string Currency);
    private sealed record CollectionResponse(string State, IReadOnlyList<CollectionItem>? Collections);
}
