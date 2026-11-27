using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using SmartParking.OperationService.API.Controllers;
using SmartParking.OperationService.Application.Usecase.Checkout;
using SmartParking.OperationService.Infrastructure.Clients;

namespace SmartParking.OperationService.Tests;

public class CheckoutReturnUrlTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DepartureUsesPhysicalSessionAndPreservesItsExistingRetryKey(bool reserved)
    {
        var sessionId = Guid.NewGuid();
        Guid? reservationId = reserved ? Guid.NewGuid() : null;
        using var transport = new CapturePaymentRequest();
        using var http = new HttpClient(transport) { BaseAddress = new Uri("http://payment.test") };
        var client = new CheckoutPaymentClient(http, new HttpContextAccessor(), new ConfigurationBuilder().Build());
        var now = DateTimeOffset.UtcNow;
        await client.PrepareAsync(Guid.NewGuid(), Guid.NewGuid(), sessionId, reservationId, "CAR",
            now.AddHours(-1), now, "51A12345", null, default);
        using var request = JsonDocument.Parse(transport.Body!);
        var body = request.RootElement;
        Assert.Equal(sessionId, body.GetProperty("sessionId").GetGuid());
        Assert.Equal(sessionId, body.GetProperty("referenceId").GetGuid());
        Assert.Equal("PARKING_SESSION", body.GetProperty("referenceType").GetString());
        var expectedKey = reserved ? $"checkout:reservation:{reservationId:N}" : $"checkout:parking_session:{sessionId:N}";
        Assert.Equal(expectedKey, body.GetProperty("idempotencyKey").GetString());
    }

    [Theory]
    [InlineData("http://localhost:5173/operator/operation?checkoutReturn=payos&checkoutSiteId=site")]
    [InlineData("https://parking.example/operator/cashier?checkoutReturn=payos&checkoutSiteId=site")]
    [InlineData(null)]
    public async Task PayOs_ForwardsOptionalUrlsAndPreservesIdempotency(string? callbackUrl)
    {
        var orderId = Guid.NewGuid();
        const string key = "checkout:test-session";
        using var transport = new CapturePaymentRequest();
        using var http = new HttpClient(transport) { BaseAddress = new Uri("http://payment.test") };
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer test-operator";
        var client = new CheckoutPaymentClient(http, new HttpContextAccessor { HttpContext = context },
            new ConfigurationBuilder().Build());
        var mediator = DispatchProxy.Create<IMediator, CheckoutMediator>();
        ((CheckoutMediator)mediator).Handler = new InitiateCheckoutCommandHandler(client);
        var controller = new CheckoutController(mediator, null!);
        // Use the same camelCase binding as the HTTP endpoint, including legacy requests without URLs.
        var json = callbackUrl is null ? JsonSerializer.Serialize(new { idempotencyKey = key })
            : JsonSerializer.Serialize(new { idempotencyKey = key, returnUrl = callbackUrl, cancelUrl = callbackUrl });
        var body = JsonSerializer.Deserialize<CheckoutController.IdempotencyRequest>(json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        await controller.PayOs(orderId, body, CancellationToken.None);

        Assert.Equal($"http://payment.test/api/v1/payments/orders/{orderId}/checkout", transport.Url);
        Assert.Equal(key, transport.IdempotencyKey);
        Assert.Equal("Bearer test-operator", transport.Authorization);
        var sent = JsonDocument.Parse(transport.Body!).RootElement;
        Assert.Equal("PAYOS", sent.GetProperty("paymentMethod").GetString());
        Assert.Equal(callbackUrl, sent.GetProperty("returnUrl").GetString());
        Assert.Equal(callbackUrl, sent.GetProperty("cancelUrl").GetString());
    }

    public class CheckoutMediator : DispatchProxy
    {
        public InitiateCheckoutCommandHandler Handler { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            Handler.Handle((InitiateCheckoutCommand)args![0]!, (CancellationToken)args[1]!);
    }

    private sealed class CapturePaymentRequest : HttpMessageHandler
    {
        public string? Url { get; private set; }
        public string? Body { get; private set; }
        public string? IdempotencyKey { get; private set; }
        public string? Authorization { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Url = request.RequestUri!.AbsoluteUri;
            Body = await request.Content!.ReadAsStringAsync(ct);
            if (request.RequestUri.AbsolutePath.EndsWith("/orders/prepare", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { success = true,
                    data = new { orderId = Guid.NewGuid(), orderCode = "12345", finalAmount = 2000,
                        status = "PENDING", currency = "VND", expiresAt = DateTimeOffset.UtcNow.AddMinutes(10) } }) };
            IdempotencyKey = request.Headers.GetValues("Idempotency-Key").Single();
            Authorization = request.Headers.Authorization?.ToString();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { success = true, data = new {
                    paymentTransactionId = Guid.NewGuid(), orderCode = 12345, amount = 2000,
                    checkoutUrl = "https://pay.payos.vn/web/test", status = "PENDING",
                    expiresAt = DateTimeOffset.UtcNow.AddMinutes(10)
                } })
            };
        }
    }
}
