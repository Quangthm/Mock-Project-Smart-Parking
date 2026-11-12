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
    public async Task QuoteAsync_ReadsPaymentEnvelopeData()
    {
        var selection = NewSelection();
        var handler = new StubHttpMessageHandler(_ => Json(Serialize(new
        {
            success = true,
            data = new
            {
                quoteId = Guid.NewGuid(),
                version = "v1",
                validUntil = DateTimeOffset.UtcNow.AddMinutes(10),
                selection = new
                {
                    vehicleId = selection.VehicleId,
                    siteId = selection.SiteId,
                    requestedSlotId = selection.RequestedSlotId,
                    start = selection.Start,
                    end = selection.End,
                    paymentMethod = selection.PaymentMethod
                },
                accountId = Guid.NewGuid(),
                bookedAmount = 50000m,
                depositAmount = 10000m,
                currency = "VND",
                prepaymentMode = "FULL",
                coverageVersion = "STANDARD_v1",
                disclosure = "Terms"
            }
        })));
        var client = CreateClient(handler);

        var quote = await client.QuoteAsync(Guid.NewGuid(), Guid.NewGuid(), selection, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, quote.QuoteId);
        Assert.Equal(selection, quote.Selection);
        Assert.Equal(50000m, quote.BookedAmount);
        Assert.Equal(10000m, quote.DepositAmount);
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
        var terms = new CommercialTerms(Guid.NewGuid(), "v1", DateTimeOffset.UtcNow.AddMinutes(10),
            selection, Guid.NewGuid(), 50000m, 10000m, "VND", "FULL", "STANDARD_v1", "Terms");
        return new DepositOrderRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            selection.SiteId, selection.VehicleId, selection.RequestedSlotId, selection.Start, selection.End,
            Guid.NewGuid(), "ops-v1", terms, DateTimeOffset.UtcNow.AddMinutes(10));
    }

    private static object Accepted(DepositOrderRequest request)
    {
        var terms = request.AcceptedTerms;
        return new
        {
            operationId = request.OperationId,
            reservationId = request.ReservationId,
            accountId = request.AccountId,
            tenantId = request.TenantId,
            siteId = request.SiteId,
            quoteId = terms.QuoteId,
            termsVersion = terms.Version,
            amount = terms.DepositAmount,
            currency = terms.Currency,
            paymentMethod = terms.Selection.PaymentMethod,
            expiresAt = request.ExpiresAt
        };
    }

    private static string Serialize(object value) => JsonSerializer.Serialize(value);

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> response)
        : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(response(request));
        }
    }
}
