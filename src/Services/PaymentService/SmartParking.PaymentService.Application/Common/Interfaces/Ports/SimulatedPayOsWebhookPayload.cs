namespace SmartParking.PaymentService.Application.Common.Interfaces.Ports;

public sealed record SimulatedPayOsWebhookPayload(
    string RawPayload,
    string Signature);
