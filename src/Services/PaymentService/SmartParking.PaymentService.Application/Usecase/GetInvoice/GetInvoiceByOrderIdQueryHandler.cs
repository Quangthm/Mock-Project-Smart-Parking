using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.PaymentService.Application.Common.Interfaces;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.Common.Mappings;
using SmartParking.PaymentService.Application.DTOs.Invoices;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Exceptions;

namespace SmartParking.PaymentService.Application.Usecase.GetInvoice;

public sealed class GetInvoiceByOrderIdQueryHandler : IRequestHandler<GetInvoiceByOrderIdQuery, CustomerInvoiceResponseDto>
{
    private readonly IPaymentOrderRepository _orderRepository;
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly ICurrentActor _currentActor;
    private readonly ILogger<GetInvoiceByOrderIdQueryHandler> _logger;

    public GetInvoiceByOrderIdQueryHandler(
        IPaymentOrderRepository orderRepository,
        IInvoiceRepository invoiceRepository,
        ICurrentActor currentActor,
        ILogger<GetInvoiceByOrderIdQueryHandler> logger)
    {
        _orderRepository = orderRepository;
        _invoiceRepository = invoiceRepository;
        _currentActor = currentActor;
        _logger = logger;
    }

    public async Task<CustomerInvoiceResponseDto> Handle(GetInvoiceByOrderIdQuery request, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order == null)
        {
            _logger.LogWarning("PaymentOrder {OrderId} not found.", request.OrderId);
            throw new PaymentDomainException(PaymentDomainException.OrderNotFound, $"Không tìm thấy đơn hàng thanh toán {request.OrderId}.");
        }

        if (order.Status == PaymentOrderStatus.PENDING)
        {
            _logger.LogWarning("Order {OrderId} is still PENDING. Cannot retrieve final authoritative invoice.", order.Id);
            throw new PaymentDomainException(
                PaymentDomainException.UnsettledPaymentOrder,
                "Yêu cầu đọc hóa đơn chính thức khi đơn hàng vẫn đang PENDING. Tiếp tục chờ Webhook hoặc tra cứu trạng thái thanh toán.");
        }

        var invoice = await _invoiceRepository.GetByPaymentOrderIdAsync(order.Id, cancellationToken);
        if (invoice == null)
        {
            _logger.LogWarning("Invoice for order {OrderId} not found.", request.OrderId);
            throw new PaymentDomainException(PaymentDomainException.InvoiceNotFound, "Không tìm thấy hóa đơn tài chính tương ứng với đơn hàng.");
        }

        InvoiceAuthorizationValidator.ValidateAccess(invoice, order, _currentActor);

        return InvoiceDtoMapper.MapToDto(invoice, order);
    }
}
