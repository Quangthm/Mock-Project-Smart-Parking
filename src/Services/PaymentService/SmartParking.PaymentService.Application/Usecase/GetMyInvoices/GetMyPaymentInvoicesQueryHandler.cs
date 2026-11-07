using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.PaymentService.Application.Common.Interfaces;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.Common.Mappings;
using SmartParking.PaymentService.Application.DTOs.Invoices;
using SmartParking.PaymentService.Domain.Exceptions;

namespace SmartParking.PaymentService.Application.Usecase.GetMyInvoices;

public sealed class GetMyPaymentInvoicesQueryHandler : IRequestHandler<GetMyPaymentInvoicesQuery, IReadOnlyList<CustomerInvoiceResponseDto>>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly ICurrentActor _currentActor;
    private readonly ILogger<GetMyPaymentInvoicesQueryHandler> _logger;

    public GetMyPaymentInvoicesQueryHandler(
        IInvoiceRepository invoiceRepository,
        ICurrentActor currentActor,
        ILogger<GetMyPaymentInvoicesQueryHandler> logger)
    {
        _invoiceRepository = invoiceRepository;
        _currentActor = currentActor;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CustomerInvoiceResponseDto>> Handle(GetMyPaymentInvoicesQuery request, CancellationToken cancellationToken)
    {
        if (!_currentActor.IsAuthenticated || !_currentActor.UserId.HasValue || _currentActor.UserId.Value == Guid.Empty)
        {
            _logger.LogWarning("Unauthenticated or missing actor UserId when querying driver invoice history.");
            throw new PaymentDomainException(
                PaymentDomainException.Unauthorized,
                "Thiếu token xác thực hoặc token không hợp lệ.");
        }

        if (!string.Equals(_currentActor.Role, "DRIVER", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(_currentActor.Role, "CUSTOMER", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Actor role {Role} not permitted to access driver invoice history.", _currentActor.Role);
            throw new PaymentDomainException(
                PaymentDomainException.ForbiddenInvoiceAccess,
                "Chỉ tài xế hoặc khách hàng mới có quyền truy cập lịch sử hóa đơn cá nhân.");
        }

        var accountId = _currentActor.UserId.Value;
        var invoices = await _invoiceRepository.GetByAccountIdAsync(accountId, cancellationToken);

        _logger.LogInformation("Retrieved {Count} invoices for driver account {AccountId}.", invoices.Count, accountId);

        return invoices
            .Select(inv => InvoiceDtoMapper.MapToDto(inv, inv.PaymentOrder))
            .ToList();
    }
}
