using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.PaymentService.Application.Common.Interfaces;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.Common.Mappings;
using SmartParking.PaymentService.Application.DTOs.Invoices;
using SmartParking.PaymentService.Domain.Exceptions;

namespace SmartParking.PaymentService.Application.Usecase.GetInvoice;

public sealed class GetInvoiceByIdQueryHandler : IRequestHandler<GetInvoiceByIdQuery, CustomerInvoiceResponseDto>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IPaymentOrderRepository _orderRepository;
    private readonly ICurrentActor _currentActor;
    private readonly ILogger<GetInvoiceByIdQueryHandler> _logger;

    public GetInvoiceByIdQueryHandler(
        IInvoiceRepository invoiceRepository,
        IPaymentOrderRepository orderRepository,
        ICurrentActor currentActor,
        ILogger<GetInvoiceByIdQueryHandler> logger)
    {
        _invoiceRepository = invoiceRepository;
        _orderRepository = orderRepository;
        _currentActor = currentActor;
        _logger = logger;
    }

    public async Task<CustomerInvoiceResponseDto> Handle(GetInvoiceByIdQuery request, CancellationToken cancellationToken)
    {
        var invoice = await _invoiceRepository.GetByIdAsync(request.InvoiceId, cancellationToken);
        if (invoice == null)
        {
            _logger.LogWarning("Invoice {InvoiceId} not found.", request.InvoiceId);
            throw new PaymentDomainException(PaymentDomainException.InvoiceNotFound, "No invoice was found for the requested identifier.");
        }

        var order = await _orderRepository.GetByIdAsync(invoice.PaymentOrderId, cancellationToken);

        InvoiceAuthorizationValidator.ValidateAccess(invoice, order, _currentActor);

        return InvoiceDtoMapper.MapToDto(invoice, order);
    }
}
