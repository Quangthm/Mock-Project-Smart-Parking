using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.PaymentService.Application.Common.Interfaces;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.Common.Mappings;
using SmartParking.PaymentService.Application.DTOs.Invoices;
using SmartParking.PaymentService.Domain.Exceptions;

namespace SmartParking.PaymentService.Application.Usecase.GetInvoice;

public sealed class GetInvoiceBySessionIdQueryHandler : IRequestHandler<GetInvoiceBySessionIdQuery, CustomerInvoiceResponseDto>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IPaymentOrderRepository _orderRepository;
    private readonly ICurrentActor _currentActor;
    private readonly ILogger<GetInvoiceBySessionIdQueryHandler> _logger;

    public GetInvoiceBySessionIdQueryHandler(
        IInvoiceRepository invoiceRepository,
        IPaymentOrderRepository orderRepository,
        ICurrentActor currentActor,
        ILogger<GetInvoiceBySessionIdQueryHandler> logger)
    {
        _invoiceRepository = invoiceRepository;
        _orderRepository = orderRepository;
        _currentActor = currentActor;
        _logger = logger;
    }

    public async Task<CustomerInvoiceResponseDto> Handle(GetInvoiceBySessionIdQuery request, CancellationToken cancellationToken)
    {
        var invoice = await _invoiceRepository.GetBySessionIdAsync(request.SessionId, cancellationToken);
        if (invoice == null)
        {
            _logger.LogWarning("Invoice for SessionId {SessionId} not found.", request.SessionId);
            throw new PaymentDomainException(PaymentDomainException.InvoiceNotFound, "Không tìm thấy hóa đơn tài chính tương ứng với phiên đỗ xe yêu cầu.");
        }

        var order = await _orderRepository.GetByIdAsync(invoice.PaymentOrderId, cancellationToken);

        InvoiceAuthorizationValidator.ValidateAccess(invoice, order, _currentActor);

        return InvoiceDtoMapper.MapToDto(invoice, order);
    }
}
