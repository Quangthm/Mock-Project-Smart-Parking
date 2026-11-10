using MediatR;
using SmartParking.PaymentService.Application.DTOs.Invoices;

namespace SmartParking.PaymentService.Application.Usecase.GetInvoicePdf;

public sealed record DownloadInvoicePdfQuery(Guid InvoiceId) : IRequest<InvoicePdfResultDto>;
