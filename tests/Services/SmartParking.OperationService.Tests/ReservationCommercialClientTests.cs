using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Infrastructure.Reservations;

namespace SmartParking.OperationService.Tests;

public sealed class ReservationCommercialClientTests
{
    [Fact]
    public async Task PreviewAsync_ReadsPaymentEnvelopeData()
    {
        var handler = new StubHttpMessageHandler(_ => Json(Serialize(new
        {
            success = true,
            data = new
            {
                bookedAmount = 50000m,
                depositAmount = 10000m,
                currency = "VND",
                pricingVersion = "v1",
                requiresCompleteSelection = false
            }
        })));
        var client = CreateClient(handler);

        var preview = await client.PreviewAsync(Guid.NewGuid(),
            new ReservationPricePreview(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddHours(1), Guid.NewGuid(), "PAYOS"), CancellationToken.None);

        Assert.Equal(50000m, preview.BookedAmount);
        Assert.Equal(10000m, preview.DepositAmount);
        Assert.Equal("VND", preview.Currency);
        Assert.Equal("/api/v1/pricing/reservations/preview", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task EnsureOrderAsync_UsesSignedOperationsTokenWithoutDriverContext()
    {
        using var rsa = RSA.Create(2048);
        DepositOrderRequest? sent = null;
        var handler = new StubHttpMessageHandler(_ => Json(Serialize(new
        {
            success = true,
            data = new
            {
                state = "CREATED",
                orderId = Guid.NewGuid(),
                acceptedRequest = Accepted(sent!)
            }
        })));
        var client = CreateClient(handler, rsa.ExportRSAPrivateKeyPem());
        var request = NewDepositOrderRequest();
        sent = request;

        var result = await client.EnsureOrderAsync(request, CancellationToken.None);

        using var body = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal(request.QuoteId, body.RootElement.GetProperty("quoteId").GetGuid());
        Assert.Equal(request.QuoteVersion, body.RootElement.GetProperty("termsVersion").GetString());
        Assert.False(body.RootElement.TryGetProperty("operationsTermsId", out _));
        Assert.Equal("CREATED", result.State);
        Assert.NotNull(result.OrderId);
        Assert.NotEqual(Guid.Empty, result.OrderId.Value);
        Assert.False(handler.LastRequest!.Headers.Contains("X-Internal-Service-Key"));
        var authorization = handler.LastRequest.Headers.Authorization;
        Assert.Equal("Bearer", authorization?.Scheme);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(authorization!.Parameter);
        Assert.Equal("SmartParking.OperationService", token.Issuer);
        Assert.Contains("SmartParking.PaymentService", token.Audiences);
        Assert.Contains(token.Claims, claim => claim.Type == "permission" && claim.Value == "reservation:deposit-order");
        Assert.Contains(token.Claims, claim => claim.Type == "role" && claim.Value == "INTERNAL_SERVICE");
    }

    [Fact]
    public async Task EnsureOrderAsync_FailsClosedWhenMachineAuthIsNotConfigured()
    {
        var handler = new StubHttpMessageHandler(_ => throw new Xunit.Sdk.XunitException("HTTP must not be called."));
        var client = CreateClient(handler);
        var request = NewDepositOrderRequest();

        var error = await Assert.ThrowsAsync<ReservationGateException>(
            () => client.EnsureOrderAsync(request, CancellationToken.None));

        Assert.Equal(503, error.StatusCode);
        Assert.Equal("OWNER_UNAVAILABLE", error.Code);
        Assert.Null(handler.LastRequest);
    }

    private static ReservationCommercialClient CreateClient(
        HttpMessageHandler handler,
        string? privateKeyPem = null,
        HttpContext? httpContext = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ReservationIntegration:Enabled"] = privateKeyPem is null ? "false" : "true",
            ["ReservationIntegration:AllowHttp"] = "true",
            ["ReservationIntegration:OperationsPrivateKeyPem"] = privateKeyPem
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var context = new HttpContextAccessor { HttpContext = httpContext };
        return new ReservationCommercialClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5065/") }, context, configuration);
    }

    private static BookingSelection NewSelection()
    {
        var now = DateTimeOffset.UtcNow;
        return new BookingSelection(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now, now.AddHours(1), "PAYOS");
    }

    private static DepositOrderRequest NewDepositOrderRequest()
    {
        var selection = NewSelection();
        return new DepositOrderRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            selection.SiteId, Guid.NewGuid(), "v1", 10000m, "VND", selection.PaymentMethod,
            DateTimeOffset.UtcNow.AddMinutes(10));
    }

    private static object Accepted(DepositOrderRequest request)
    {
        return new
        {
            operationId = request.OperationId,
            reservationId = request.ReservationId,
            accountId = request.AccountId,
            tenantId = request.TenantId,
            siteId = request.SiteId,
            quoteId = request.QuoteId,
            termsVersion = request.QuoteVersion,
            amount = request.Amount,
            currency = request.Currency,
            paymentMethod = request.PaymentMethod,
            expiresAt = request.ExpiresAt
        };
    }

    private static string Serialize(object value) => JsonSerializer.Serialize(value);

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}
