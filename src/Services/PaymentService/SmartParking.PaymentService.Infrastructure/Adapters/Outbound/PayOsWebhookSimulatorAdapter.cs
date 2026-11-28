using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Infrastructure.Options;
using SmartParking.PaymentService.Infrastructure.Services;

namespace SmartParking.PaymentService.Infrastructure.Adapters.Outbound;

public sealed class PayOsWebhookSimulatorAdapter : IPayOsWebhookSimulatorPort
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);
    private readonly PayOsOptions _options;

    public PayOsWebhookSimulatorAdapter(IOptions<PayOsOptions> options)
    {
        _options = options.Value;
    }

    public SimulatedPayOsWebhookPayload BuildSignedSuccessWebhook(DemoPayOsWebhookRequest request)
    {
        var reference = $"DEMO-{request.OrderId:N}";
        var paymentLinkId = string.IsNullOrWhiteSpace(request.ExistingGatewayTransactionId)
            ? $"DEMO-LINK-{request.OrderId:N}"
            : request.ExistingGatewayTransactionId.Trim();

        var data = new Dictionary<string, object?>
        {
            ["orderCode"] = request.OrderCode,
            ["amount"] = request.AmountVnd,
            ["description"] = $"SP {request.OrderCode}",
            ["reference"] = reference,
            ["paymentLinkId"] = paymentLinkId,
            ["transactionDateTime"] = request.OccurredAt
                .ToOffset(VietnamOffset)
                .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            ["currency"] = "VND",
            ["code"] = "00",
            ["desc"] = "success"
        };

        var signature = PayOsSignatureService.CreateSignatureFromDictionary(
            data,
            _options.ChecksumKey ?? string.Empty);

        var rawPayload = JsonSerializer.Serialize(new
        {
            code = "00",
            desc = "success",
            success = true,
            data,
            signature
        });

        return new SimulatedPayOsWebhookPayload(rawPayload, signature);
    }
}
