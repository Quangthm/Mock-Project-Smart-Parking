using MediatR;
using SmartParking.PaymentService.Application.DTOs.Invoices;

namespace SmartParking.PaymentService.Application.Usecase.GetInvoice;

public sealed record GetInvoiceByOrderIdQuery(Guid OrderId) : IRequest<CustomerInvoiceResponseDto>;
