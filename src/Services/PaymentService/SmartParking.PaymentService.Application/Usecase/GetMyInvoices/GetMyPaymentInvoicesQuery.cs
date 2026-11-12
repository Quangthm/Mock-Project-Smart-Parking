using MediatR;
using SmartParking.PaymentService.Application.DTOs.Invoices;

namespace SmartParking.PaymentService.Application.Usecase.GetMyInvoices;

public sealed record GetMyPaymentInvoicesQuery(
    string? Status = null,
    DateTimeOffset? FromDate = null,
    DateTimeOffset? ToDate = null,
    int PageNumber = 1,
    int PageSize = 10
) : IRequest<PagedResult<CustomerInvoiceResponseDto>>;
