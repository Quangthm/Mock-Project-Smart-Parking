using MediatR;
using SmartParking.PaymentService.Application.DTOs.Invoices;

namespace SmartParking.PaymentService.Application.Usecase.GetMyInvoices;

public sealed record GetMyPaymentInvoicesQuery : IRequest<IReadOnlyList<CustomerInvoiceResponseDto>>;
