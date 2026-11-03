using MediatR;
using SmartParking.PaymentService.Application.DTOs.Invoices;

namespace SmartParking.PaymentService.Application.Usecase.GetInvoice;

public sealed record GetInvoiceBySessionIdQuery(Guid SessionId) : IRequest<CustomerInvoiceResponseDto>;
