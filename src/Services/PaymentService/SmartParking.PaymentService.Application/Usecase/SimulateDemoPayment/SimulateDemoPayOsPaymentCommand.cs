using MediatR;
using SmartParking.PaymentService.Application.DTOs.Payments;

namespace SmartParking.PaymentService.Application.Usecase.SimulateDemoPayment;

public sealed record SimulateDemoPayOsPaymentCommand(Guid OrderId)
    : IRequest<PaymentTransactionStatusDto>;
