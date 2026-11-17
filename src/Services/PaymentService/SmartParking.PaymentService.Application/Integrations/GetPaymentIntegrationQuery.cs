using MediatR;

namespace SmartParking.PaymentService.Application.Integrations;

/// <summary>
/// Reads the current PayOS payment integration configuration for the active mode.
/// </summary>
public sealed record GetPaymentIntegrationQuery : IRequest<PaymentIntegrationQueryDto>;
