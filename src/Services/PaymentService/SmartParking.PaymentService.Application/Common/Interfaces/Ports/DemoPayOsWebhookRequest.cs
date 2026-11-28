namespace SmartParking.PaymentService.Application.Common.Interfaces.Ports;

public sealed record DemoPayOsWebhookRequest(
    Guid OrderId,
    long OrderCode,
    long AmountVnd,
    string? ExistingGatewayTransactionId,
    DateTimeOffset OccurredAt);
