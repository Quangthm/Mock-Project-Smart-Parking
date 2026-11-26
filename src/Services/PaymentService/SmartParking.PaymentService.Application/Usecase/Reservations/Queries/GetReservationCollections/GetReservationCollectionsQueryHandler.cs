using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.DTOs.Reservations;
using SmartParking.PaymentService.Domain.Enums;

namespace SmartParking.PaymentService.Application.Usecase.Reservations.Queries.GetReservationCollections;

public sealed class GetReservationCollectionsQueryHandler
    : IRequestHandler<GetReservationCollectionsQuery, CollectionLookupResponseDto>
{
    private readonly IPaymentOrderRepository _orderRepository;
    private readonly IPaymentTransactionRepository _transactionRepository;
    private readonly ILogger<GetReservationCollectionsQueryHandler> _logger;

    public GetReservationCollectionsQueryHandler(
        IPaymentOrderRepository orderRepository,
        IPaymentTransactionRepository transactionRepository,
        ILogger<GetReservationCollectionsQueryHandler> logger)
    {
        _orderRepository = orderRepository;
        _transactionRepository = transactionRepository;
        _logger = logger;
    }

    public async Task<CollectionLookupResponseDto> Handle(
        GetReservationCollectionsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Querying collections for OrderId: {OrderId}", query.OrderId);

        var order = await _orderRepository.GetByIdAsync(query.OrderId, cancellationToken);
        if (order == null)
        {
            _logger.LogWarning("Order '{OrderId}' not found for collections lookup.", query.OrderId);
            return new CollectionLookupResponseDto("CLOSED_NO_COLLECTION", Array.Empty<VerifiedCollectionDto>());
        }

        var transactions = await _transactionRepository.GetByOrderIdAsync(order.Id, cancellationToken);
        var successTransactions = transactions.Where(t => t.Status == PaymentStatus.SUCCESS).ToList();

        if (successTransactions.Count > 0)
        {
            var collections = successTransactions.Select(tx => new VerifiedCollectionDto(
                CollectionId: tx.Id,
                OrderId: order.Id,
                ReservationId: order.ReferenceId,
                AccountId: order.AccountId ?? Guid.Empty,
                TenantId: order.TenantId,
                SiteId: order.SiteId,
                QuoteId: Guid.Empty,
                TermsVersion: "1",
                Amount: tx.Amount,
                Currency: "VND"
            )).ToList();

            return new CollectionLookupResponseDto("SETTLED", collections);
        }

        if (transactions.Any(t => t.Status == PaymentStatus.UNKNOWN))
        {
            return new CollectionLookupResponseDto("UNKNOWN", Array.Empty<VerifiedCollectionDto>());
        }

        if (transactions.Any(t => t.Status == PaymentStatus.PENDING))
        {
            return new CollectionLookupResponseDto("PENDING", Array.Empty<VerifiedCollectionDto>());
        }

        return new CollectionLookupResponseDto("CLOSED_NO_COLLECTION", Array.Empty<VerifiedCollectionDto>());
    }
}
