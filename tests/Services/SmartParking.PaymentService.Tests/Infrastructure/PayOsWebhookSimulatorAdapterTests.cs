using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Infrastructure.Adapters.Outbound;
using SmartParking.PaymentService.Infrastructure.Options;

namespace SmartParking.PaymentService.Tests.Infrastructure;

public class PayOsWebhookSimulatorAdapterTests
{
    [Fact]
    public async Task BuildSignedSuccessWebhook_RoundTripsThroughPayOsAdapterVerification()
    {
        var options = Options.Create(new PayOsOptions { ChecksumKey = "demo-test-checksum-key-123" });
        var simulator = new PayOsWebhookSimulatorAdapter(options);
        var payOsAdapter = new PayOsAdapter(new HttpClient(), options, NullLogger<PayOsAdapter>.Instance);

        var orderId = Guid.Parse("9c1b7e42-6a12-4d9c-9c45-12a34b56c789");
        var occurredAt = new DateTimeOffset(2026, 11, 27, 4, 10, 0, TimeSpan.Zero);

        var simulated = simulator.BuildSignedSuccessWebhook(new DemoPayOsWebhookRequest(
            OrderId: orderId,
            OrderCode: 1795664800123456L,
            AmountVnd: 25000L,
            ExistingGatewayTransactionId: null,
            OccurredAt: occurredAt));

        var verified = await payOsAdapter.VerifyAndParseWebhookAsync(
            simulated.RawPayload,
            simulated.Signature);

        Assert.True(verified.IsValidSignature);
        Assert.Equal("SUCCESS", verified.PaymentStatus);
        Assert.Equal(1795664800123456L, verified.OrderCode);
        Assert.Equal(25000L, verified.AmountVnd);
        Assert.Equal($"DEMO-{orderId:N}", verified.BankReference);
        Assert.Equal($"DEMO-LINK-{orderId:N}", verified.GatewayTransactionId);
    }

    [Fact]
    public async Task BuildSignedSuccessWebhook_PreservesExistingGatewayTransactionId()
    {
        var options = Options.Create(new PayOsOptions { ChecksumKey = "secret-key-xyz" });
        var simulator = new PayOsWebhookSimulatorAdapter(options);
        var payOsAdapter = new PayOsAdapter(new HttpClient(), options, NullLogger<PayOsAdapter>.Instance);

        var orderId = Guid.NewGuid();
        var simulated = simulator.BuildSignedSuccessWebhook(new DemoPayOsWebhookRequest(
            OrderId: orderId,
            OrderCode: 202611270001L,
            AmountVnd: 50000L,
            ExistingGatewayTransactionId: "EXISTING-LINK-999",
            OccurredAt: DateTimeOffset.UtcNow));

        var verified = await payOsAdapter.VerifyAndParseWebhookAsync(
            simulated.RawPayload,
            simulated.Signature);

        Assert.True(verified.IsValidSignature);
        Assert.Equal("EXISTING-LINK-999", verified.GatewayTransactionId);
        Assert.Equal($"DEMO-{orderId:N}", verified.BankReference);
    }
}
