using MediatR;

namespace SmartParking.PaymentService.Application.Usecase.ProcessWebhook;

public record ProcessPayOsWebhookResult(
    bool Success,
    string Message,
    string? ErrorCode = null
);

public record ProcessPayOsWebhookCommand(
    string RawPayload,
    string Signature
) : IRequest<ProcessPayOsWebhookResult>;
