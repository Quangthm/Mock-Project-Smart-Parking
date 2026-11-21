using MediatR;
using SmartParking.PaymentService.Application.DTOs.Payments;

namespace SmartParking.PaymentService.Application.Usecase.CreatePaymentOrder;

public sealed record CreatePaymentOrderCommand(CreatePaymentOrderRequestDto Request)
    : IRequest<PaymentOrderResponseDto>;
