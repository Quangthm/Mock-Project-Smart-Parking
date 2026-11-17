using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.DTOs.Reservations;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Exceptions;

namespace SmartParking.PaymentService.Application.Usecase.Reservations.Commands.EnsureReservationDepositOrder;

public sealed class EnsureReservationDepositOrderCommandHandler
    : IRequestHandler<EnsureReservationDepositOrderCommand, EnsureReservationDepositOrderResponseDto>
{
    private readonly IPaymentOrderRepository _orderRepository;
    private readonly IReservationQuoteRepository _quoteRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EnsureReservationDepositOrderCommandHandler> _logger;

    public EnsureReservationDepositOrderCommandHandler(
        IPaymentOrderRepository orderRepository,
        IReservationQuoteRepository quoteRepository,
        IUnitOfWork unitOfWork,
        ILogger<EnsureReservationDepositOrderCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _quoteRepository = quoteRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<EnsureReservationDepositOrderResponseDto> Handle(
        EnsureReservationDepositOrderCommand command,
        CancellationToken cancellationToken)
    {
        var req = command.Request;
        var now = DateTimeOffset.UtcNow;
        var opKey = req.OperationId.ToString();

        // 1. Idempotency Check by OperationId
        var existingOrder = await _orderRepository.GetByIdempotencyKeyAsync(opKey, cancellationToken);
        if (existingOrder != null)
        {
            if (existingOrder.ReferenceId != req.ReservationId ||
                existingOrder.AccountId != req.AccountId ||
                existingOrder.TenantId != req.TenantId ||
                existingOrder.SiteId != req.SiteId ||
                existingOrder.ReferenceType != ReferenceType.RESERVATION ||
                existingOrder.OrderType != OrderType.RESERVATION_FEE ||
                existingOrder.ExpiresAt != req.ExpiresAt ||
                existingOrder.QuoteId != req.QuoteId ||
                !string.Equals(existingOrder.TermsVersion, req.TermsVersion, StringComparison.Ordinal) ||
                Math.Abs(existingOrder.Amount - req.Amount) > 0.01m ||
                Math.Abs(existingOrder.FinalAmount - req.Amount) > 0.01m)
            {
                _logger.LogWarning("Idempotency conflict for OperationId: {OperationId}", req.OperationId);
                throw new PaymentDomainException(
                    "IDEMPOTENCY_PAYLOAD_MISMATCH",
                    $"OperationId '{req.OperationId}' was previously executed with a different payload.",
                    "operationId");
            }

            if (existingOrder.Status == PaymentOrderStatus.CANCELLED || existingOrder.IsExpired(now))
            {
                _logger.LogInformation("OperationId: {OperationId} was closed with no order (Tombstone / Expired).", req.OperationId);
                return new EnsureReservationDepositOrderResponseDto("CLOSED_NO_ORDER", null,
                    PersistedAcceptedRequest(existingOrder, req));
            }

            _logger.LogInformation("Replaying existing order '{OrderId}' for OperationId: {OperationId}", existingOrder.Id, req.OperationId);
            return new EnsureReservationDepositOrderResponseDto("CREATED", existingOrder.Id,
                PersistedAcceptedRequest(existingOrder, req));
        }

        // 2. Fence Check: If now >= ExpiresAt (H-Fence Tombstone)
        if (now >= req.ExpiresAt)
        {
            _logger.LogWarning("OperationId: {OperationId} arrived at/after H ({ExpiresAt}). Applying durable tombstone.", req.OperationId, req.ExpiresAt);
            var tombstone = new PaymentOrder
            {
                Id = Guid.NewGuid(),
                OrderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString() + Random.Shared.Next(100, 999).ToString(),
                TenantId = req.TenantId,
                SiteId = req.SiteId,
                AccountId = req.AccountId,
                OrderType = OrderType.RESERVATION_FEE,
                ReferenceType = ReferenceType.RESERVATION,
                ReferenceId = req.ReservationId,
                QuoteId = req.QuoteId,
                TermsVersion = req.TermsVersion,
                Amount = req.Amount,
                FinalAmount = req.Amount,
                Status = PaymentOrderStatus.CANCELLED,
                IdempotencyKey = opKey,
                ExpiresAt = req.ExpiresAt,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _orderRepository.AddAsync(tombstone, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new EnsureReservationDepositOrderResponseDto("CLOSED_NO_ORDER", null,
                PersistedAcceptedRequest(tombstone, req));
        }

        // 3. Validate Quote
        var quote = await _quoteRepository.GetByIdAsync(req.QuoteId, cancellationToken);
        if (quote == null)
        {
            _logger.LogWarning("QuoteId '{QuoteId}' not found.", req.QuoteId);
            throw new PaymentDomainException("QUOTE_NOT_FOUND", $"ReservationQuote '{req.QuoteId}' not found.", "quoteId");
        }

        if (quote.IsExpired(now))
        {
            _logger.LogWarning("QuoteId '{QuoteId}' has expired.", req.QuoteId);
            throw new PaymentDomainException("TERMS_STALE", $"ReservationQuote '{req.QuoteId}' has expired.", "quoteId");
        }

        if (quote.Status != ReservationQuoteStatus.ISSUED)
        {
            _logger.LogWarning("QuoteId '{QuoteId}' has invalid status: {Status}", req.QuoteId, quote.Status);
            throw new PaymentDomainException("TERMS_STALE", $"ReservationQuote '{req.QuoteId}' has status '{quote.Status}', expected ISSUED.", "quoteId");
        }

        if (quote.AccountId != req.AccountId)
        {
            _logger.LogWarning("Quote AccountId mismatch: Quote={QuoteAccount}, Req={ReqAccount}", quote.AccountId, req.AccountId);
            throw new PaymentDomainException("QUOTE_ACCOUNT_MISMATCH", "ReservationQuote does not belong to the requested account.", "accountId");
        }

        if (quote.DepositAmount != req.Amount)
        {
            _logger.LogWarning("Quote DepositAmount mismatch: Quote={QuoteAmount}, Req={ReqAmount}", quote.DepositAmount, req.Amount);
            throw new PaymentDomainException("QUOTE_AMOUNT_MISMATCH", $"Requested amount {req.Amount} does not match quote deposit {quote.DepositAmount}.", "amount");
        }

        if (req.TenantId != Guid.Empty && quote.TenantId != Guid.Empty && quote.TenantId != req.TenantId)
        {
            _logger.LogWarning("Quote TenantId mismatch: Quote={QuoteTenant}, Req={ReqTenant}", quote.TenantId, req.TenantId);
            throw new PaymentDomainException("QUOTE_TENANT_MISMATCH", "ReservationQuote does not belong to the requested tenant.", "tenantId");
        }

        if (quote.SiteId != req.SiteId)
        {
            _logger.LogWarning("Quote SiteId mismatch: Quote={QuoteSite}, Req={ReqSite}", quote.SiteId, req.SiteId);
            throw new PaymentDomainException("QUOTE_SITE_MISMATCH", "ReservationQuote does not belong to the requested site.", "siteId");
        }

        if (!string.Equals(quote.Currency, req.Currency, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Quote currency mismatch: Quote={QuoteCurrency}, Req={ReqCurrency}", quote.Currency, req.Currency);
            throw new PaymentDomainException("QUOTE_CURRENCY_MISMATCH", "ReservationQuote currency does not match the requested currency.", "currency");
        }

        if (!string.Equals(quote.TermsVersion, req.TermsVersion, StringComparison.Ordinal))
        {
            _logger.LogWarning("Quote terms version mismatch: Quote={QuoteVersion}, Req={ReqVersion}", quote.TermsVersion, req.TermsVersion);
            throw new PaymentDomainException("TERMS_STALE", "ReservationQuote terms version does not match the accepted terms.", "termsVersion");
        }

        // 4. Accept Quote & Create Order atomically
        quote.MarkAccepted();
        _quoteRepository.Update(quote);

        var orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString() + Random.Shared.Next(100, 999).ToString();
        var order = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            OrderCode = orderCode,
            TenantId = req.TenantId,
            SiteId = req.SiteId,
            AccountId = req.AccountId,
            OrderType = OrderType.RESERVATION_FEE,
            ReferenceType = ReferenceType.RESERVATION,
            ReferenceId = req.ReservationId,
            QuoteId = quote.Id,
            TermsVersion = quote.TermsVersion,
            Amount = req.Amount,
            FinalAmount = req.Amount,
            Status = PaymentOrderStatus.PENDING,
            IdempotencyKey = opKey,
            ExpiresAt = req.ExpiresAt,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _orderRepository.AddAsync(order, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successfully ensured reservation deposit order '{OrderId}' for OperationId '{OperationId}', Quote '{QuoteId}'",
            order.Id, req.OperationId, quote.Id);

        return new EnsureReservationDepositOrderResponseDto("CREATED", order.Id,
            PersistedAcceptedRequest(order, req));
    }

    private static EnsureReservationDepositOrderRequestDto PersistedAcceptedRequest(
        PaymentOrder order,
        EnsureReservationDepositOrderRequestDto requested)
    {
        var operationId = Guid.TryParse(order.IdempotencyKey, out var parsed)
            ? parsed
            : requested.OperationId;
        return new EnsureReservationDepositOrderRequestDto(
            operationId,
            order.ReferenceId,
            order.AccountId ?? Guid.Empty,
            order.TenantId,
            order.SiteId,
            order.QuoteId ?? Guid.Empty,
            order.TermsVersion ?? string.Empty,
            order.Amount,
            requested.Currency,
            requested.PaymentMethod,
            order.ExpiresAt ?? requested.ExpiresAt);
    }
}
