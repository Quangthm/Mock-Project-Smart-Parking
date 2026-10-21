using MediatR;
using SmartParking.PaymentService.Application.DTOs.Payments;
using SmartParking.PaymentService.Domain.Enums;

namespace SmartParking.PaymentService.Application.Usecase.InitiateCheckout;

public record InitiatePaymentCheckoutCommand(
    Guid OrderId,
    string? IdempotencyKey,
    PaymentMethod PaymentMethod = PaymentMethod.PAYOS,
    string? ReturnUrl = null,
    string? CancelUrl = null
) : IRequest<PaymentCheckoutResponseDto>;
