using MediatR;
using SmartParking.PaymentService.Application.DTOs.Invoices;

namespace SmartParking.PaymentService.Application.Usecase.CollectCashPayment;

public sealed record CollectCashPaymentCommand(
    Guid OrderId,
    decimal? AmountReceived = null,
    string? Notes = null,
    string? IdempotencyKey = null
) : IRequest<CustomerInvoiceResponseDto>;
