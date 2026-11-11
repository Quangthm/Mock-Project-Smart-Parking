using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.PaymentService.Application.Common.Interfaces;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.Common.Mappings;
using SmartParking.PaymentService.Application.DTOs.Invoices;
using SmartParking.PaymentService.Domain.Exceptions;

namespace SmartParking.PaymentService.Application.Usecase.GetMyInvoices;

public sealed class GetMyPaymentInvoicesQueryHandler : IRequestHandler<GetMyPaymentInvoicesQuery, PagedResult<CustomerInvoiceResponseDto>>
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

    public async Task<PagedResult<CustomerInvoiceResponseDto>> Handle(GetMyPaymentInvoicesQuery request, CancellationToken cancellationToken)
    {
        if (!_currentActor.IsAuthenticated || !_currentActor.UserId.HasValue || _currentActor.UserId.Value == Guid.Empty)
        {
            _logger.LogWarning("Unauthenticated or missing actor UserId when querying driver invoice history.");
            throw new PaymentDomainException(
                PaymentDomainException.Unauthorized,
                "The authentication token is missing or invalid.");
        }

        if (!string.Equals(_currentActor.Role, "DRIVER", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(_currentActor.Role, "CUSTOMER", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Actor role {Role} not permitted to access driver invoice history.", _currentActor.Role);
            throw new PaymentDomainException(
                PaymentDomainException.ForbiddenInvoiceAccess,
                "Only drivers and customers can access their personal invoice history.");
        }

        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize < 1 ? 10 : (request.PageSize > 100 ? 100 : request.PageSize);
        var accountId = _currentActor.UserId.Value;

        var (items, totalCount) = await _invoiceRepository.GetByAccountIdPagedAsync(
            accountId,
            request.Status,
            request.FromDate,
            request.ToDate,
            pageNumber,
            pageSize,
            cancellationToken);

        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        var dtos = items.Select(inv => InvoiceDtoMapper.MapToDto(inv, inv.PaymentOrder)).ToList();

        _logger.LogInformation("Retrieved page {Page}/{TotalPages} ({Count}/{TotalCount} items) for driver account {AccountId}.",
            pageNumber, totalPages, dtos.Count, totalCount, accountId);

        return new PagedResult<CustomerInvoiceResponseDto>(
            Items: dtos,
            TotalCount: totalCount,
            PageNumber: pageNumber,
            PageSize: pageSize,
            TotalPages: totalPages
        );
    }
}
