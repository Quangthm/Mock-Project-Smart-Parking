using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Application.Common.Models.Exceptions;

namespace SmartParking.OperationService.Infrastructure.Clients;

public sealed class CheckoutPaymentClient(
    HttpClient client,
    IHttpContextAccessor context,
    IConfiguration configuration) : ICheckoutPaymentClient
{
    public async Task<CheckoutPricing> CalculateAsync(
        Guid tenantId,
        Guid siteId,
        Guid referenceId,
        string referenceType,
        string vehicleType,
        DateTimeOffset entryTime,
        DateTimeOffset pricingCutoffAt,
        CancellationToken ct)
    {
        var result = await SendAsync<PaymentEnvelope<PaymentPricing>>(
            HttpMethod.Post,
            "/api/v1/pricing/calculate",
            new
            {
                tenantId,
                siteId,
                vehicleType,
                entryTime,
                exitTime = pricingCutoffAt,
                sessionId = referenceType == "PARKING_SESSION" ? referenceId : (Guid?)null,
                reservationId = referenceType == "RESERVATION" ? referenceId : (Guid?)null
            },
            ct);
        return new(result.Data.FinalAmountVnd, result.Data.Currency, result.Data.TotalParkedMinutes, result.Data.IsFreeStay);
    }

    public async Task<CheckoutPaymentPreparation> PrepareAsync(
        Guid tenantId,
        Guid siteId,
        Guid referenceId,
        string referenceType,
        string vehicleType,
        DateTimeOffset entryTime,
        DateTimeOffset pricingCutoffAt,
        string plateNumber,
        Guid? accountId,
        CancellationToken ct)
    {
        var result = await SendAsync<PaymentEnvelope<PreparedPaymentOrder>>(
            HttpMethod.Post,
            "/api/v1/payments/orders/prepare",
            new
            {
                tenantId,
                siteId,
                sessionId = referenceId,
                referenceId,
                referenceType,
                vehicleType,
                entryTime,
                pricingCutoffAt,
                plateNumber,
                accountId,
                penaltyAmount = 0,
                penaltyReason = (string?)null,
                idempotencyKey = $"checkout:{referenceType.ToLowerInvariant()}:{referenceId:N}"
            },
            ct);

        var pricing = new CheckoutPricing(
            checked((long)result.Data.FinalAmount),
            result.Data.Currency,
            result.Data.TotalParkedMinutes,
            result.Data.FinalAmount == 0);
        return new(
            pricing,
            new(result.Data.OrderId, result.Data.OrderCode, result.Data.FinalAmount, result.Data.Status, result.Data.ExpiresAt));
    }

    public async Task<CheckoutPaymentCheckout> InitiateAsync(Guid orderId, string idempotencyKey, CancellationToken ct)
    {
        var result = await SendAsync<PaymentEnvelope<PaymentCheckout>>(
            HttpMethod.Post,
            $"/api/v1/payments/orders/{orderId}/checkout",
            new { paymentMethod = "PAYOS" },
            ct,
            idempotencyKey);
        return new(result.Data.PaymentTransactionId, result.Data.OrderCode, result.Data.Amount,
            result.Data.CheckoutUrl, result.Data.Status, result.Data.ExpiresAt);
    }

    public async Task<CheckoutPaymentStatus> ConfirmCashAsync(Guid orderId, string idempotencyKey, CancellationToken ct)
    {
        var result = await SendAsync<PaymentEnvelope<PaymentStatus>>(
            HttpMethod.Post,
            $"/api/v1/payments/orders/{orderId}/cash",
            new { idempotencyKey },
            ct);
        return ToStatus(result.Data);
    }

    public async Task<CheckoutPaymentStatus> DemoPayAsync(Guid orderId, CancellationToken ct)
    {
        var result = await SendAsync<PaymentEnvelope<PaymentStatus>>(
            HttpMethod.Post,
            $"/api/v1/payments/orders/{orderId}/demo-pay",
            null,
            ct);
        return ToStatus(result.Data);
    }

    public async Task<CheckoutPaymentStatus> GetStatusAsync(Guid transactionId, CancellationToken ct)
    {
        var result = await SendAsync<PaymentEnvelope<PaymentStatus>>(
            HttpMethod.Get,
            $"/api/v1/payments/transactions/{transactionId}/status",
            null,
            ct);
        return ToStatus(result.Data);
    }

    private async Task<T> SendAsync<T>(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken ct,
        string? idempotencyKey = null)
    {
        using var request = CreateRequest(method, path);
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        if (body is not null)
            request.Content = JsonContent.Create(body);

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
                        // Keep the upstream client-error status even if its body is malformed.
                    }

                    throw new ReservationGateException(
                        (int)response.StatusCode,
                        paymentError?.ErrorCode ?? "PAYMENT_REQUEST_REJECTED",
                        paymentError?.Message ?? "Payment service rejected the checkout request.");
                }

                throw new ReservationGateException(
                    503,
                    "PAYMENT_SERVICE_ERROR",
                    "Payment service could not process the checkout request.");
            }

            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct)
                ?? throw new ReservationGateException(503, "PAYMENT_SERVICE_ERROR", "Payment service returned an empty response.");
        }
        catch (ReservationGateException)
        {
            throw;
        }
        catch (Exception error) when (error is HttpRequestException or JsonException ||
                                      error is OperationCanceledException && !ct.IsCancellationRequested)
        {
            throw new ReservationGateException(503, "PAYMENT_SERVICE_UNAVAILABLE", "Payment service is unavailable.");
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        var serviceKey = configuration["Services:Key"];
        if (!string.IsNullOrWhiteSpace(serviceKey))
            request.Headers.TryAddWithoutValidation("X-Internal-Service-Key", serviceKey);

        var authorization = context.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization))
            request.Headers.TryAddWithoutValidation("Authorization", authorization);
        return request;
    }

    private static CheckoutPaymentStatus ToStatus(PaymentStatus status) =>
        new(status.TransactionId, status.OrderId, status.Amount, status.Status, status.PaidAt, status.IsAuthoritative);

    private sealed record PaymentEnvelope<T>(bool Success, T Data);
    private sealed record PaymentErrorEnvelope(string? ErrorCode, string? Message);
    private sealed record PaymentPricing(long FinalAmountVnd, string Currency, decimal TotalParkedMinutes, bool IsFreeStay);
    private sealed record PaymentOrder(Guid OrderId, string OrderCode, decimal Amount, string Status, DateTimeOffset ExpiresAt);
    private sealed record PreparedPaymentOrder(
        Guid OrderId,
        string OrderCode,
        decimal ParkingFee,
        decimal PenaltyFee,
        decimal FinalAmount,
        string Currency,
        string Status,
        DateTimeOffset ExpiresAt,
        decimal TotalParkedMinutes = 0);
    private sealed record PaymentCheckout(Guid PaymentTransactionId, long OrderCode, long Amount, string CheckoutUrl, string Status, DateTimeOffset ExpiresAt);
    private sealed record PaymentStatus(Guid TransactionId, Guid OrderId, long Amount, string Status, DateTimeOffset? PaidAt, bool IsAuthoritative);
}
