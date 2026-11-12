using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.PaymentService.Application.Common.Interfaces;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.Common.Mappings;
using SmartParking.PaymentService.Application.DTOs.Invoices;
using SmartParking.PaymentService.Application.Usecase.GetInvoice;
using SmartParking.PaymentService.Domain.Exceptions;

namespace SmartParking.PaymentService.Application.Usecase.GetInvoicePdf;

public sealed class DownloadInvoicePdfQueryHandler : IRequestHandler<DownloadInvoicePdfQuery, InvoicePdfResultDto>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IPdfInvoiceGenerator _pdfGenerator;
    private readonly ICurrentActor _currentActor;
    private readonly ILogger<DownloadInvoicePdfQueryHandler> _logger;
    private readonly IPaymentOrderRepository? _orderRepository;

    public DownloadInvoicePdfQueryHandler(
        IInvoiceRepository invoiceRepository,
        IPdfInvoiceGenerator pdfGenerator,
        ICurrentActor currentActor,
        ILogger<DownloadInvoicePdfQueryHandler> logger)
        : this(invoiceRepository, pdfGenerator, currentActor, logger, null)
    {
    }

    public DownloadInvoicePdfQueryHandler(
        IInvoiceRepository invoiceRepository,
        IPdfInvoiceGenerator pdfGenerator,
        ICurrentActor currentActor,
        ILogger<DownloadInvoicePdfQueryHandler> logger,
        IPaymentOrderRepository? orderRepository)
    {
        _invoiceRepository = invoiceRepository;
        _pdfGenerator = pdfGenerator;
        _currentActor = currentActor;
        _logger = logger;
        _orderRepository = orderRepository;
    }

    public async Task<InvoicePdfResultDto> Handle(DownloadInvoicePdfQuery request, CancellationToken cancellationToken)
    {
        var invoice = await _invoiceRepository.GetByIdAsync(request.InvoiceId, cancellationToken);
        if (invoice == null)
        {
            _logger.LogWarning("Invoice {InvoiceId} not found for PDF download.", request.InvoiceId);
            throw new PaymentDomainException(
                PaymentDomainException.InvoiceNotFound,
                "No invoice was found for the requested identifier.");
        }

        var order = invoice.PaymentOrder ?? (_orderRepository != null
            ? await _orderRepository.GetByIdAsync(invoice.PaymentOrderId, cancellationToken)
            : null);

        // Enforce settlement guardrail (PAID only -> 422 if unsettled) and role-based ownership (403 if unauthorized)
        InvoiceAuthorizationValidator.ValidateAccess(invoice, order, _currentActor);

        var invoiceDto = InvoiceDtoMapper.MapToDto(invoice, order);
        var pdfBytes = await _pdfGenerator.GenerateInvoicePdfAsync(invoiceDto, cancellationToken);

        var fileName = string.IsNullOrWhiteSpace(invoice.InvoiceNumber)
            ? $"HD_invoice_{invoice.Id}.pdf"
            : $"HD_{invoice.InvoiceNumber}.pdf";

        _logger.LogInformation("Generated PDF invoice {InvoiceNumber} ({Bytes} bytes) for caller {UserId}.",
            invoice.InvoiceNumber, pdfBytes?.Length ?? 0, _currentActor.UserId);

        return new InvoicePdfResultDto(
            Content: pdfBytes ?? Array.Empty<byte>(),
            ContentType: "application/pdf",
            FileName: fileName
        );
    }
}
