using MediatR;

namespace SmartParking.PaymentService.Application.Usecase.ProcessWebhook;

public record ProcessPayOsWebhookCommand(
    string RawPayload,
    string Signature
) : IRequest<ProcessPayOsWebhookResult>;
