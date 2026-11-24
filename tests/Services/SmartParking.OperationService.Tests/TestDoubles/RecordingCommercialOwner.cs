using SmartParking.OperationService.Application.Reservations;

namespace SmartParking.OperationService.Tests.TestDoubles;

// Records the refund reconciliation the handler performs. Unrelated commercial members are not
// exercised by emergency reconciliation and fail loudly if they are ever called.
internal sealed class RecordingCommercialOwner : IReservationCommercialOwner
{
    public List<(Guid ReservationId, Guid OrderId)> CollectionCalls { get; } = [];
    public List<(Guid ReservationId, Guid CollectionId, string Reason)> ReconcileCalls { get; } = [];
    public IReadOnlyList<VerifiedCollection> Collections { get; set; } = [];

    // Lets a test simulate a transient PaymentService failure on the first attempt.
    public Func<VerifiedCollection, Exception?>? ReconcileFailure { get; set; }

    public Task<CollectionLookup> CollectionsAsync(Guid reservationId, Guid orderId, CancellationToken ct)
    {
        CollectionCalls.Add((reservationId, orderId));
        return Task.FromResult(new CollectionLookup("SUCCESS", Collections));
    }

    public Task ReconcileAsync(Guid reservationId, VerifiedCollection collection, string reason,
        CancellationToken ct)
    {
        if (ReconcileFailure?.Invoke(collection) is { } failure) throw failure;
        ReconcileCalls.Add((reservationId, collection.CollectionId, reason));
        return Task.CompletedTask;
    }

    public Task<PricePreview> PreviewAsync(Guid accountId, ReservationPricePreview intent, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<CommercialTerms> QuoteAsync(Guid accountId, Guid tenantId, BookingSelection intent,
        CancellationToken ct) => throw new NotSupportedException();

    public Task<DepositOrderResult> EnsureOrderAsync(DepositOrderRequest request, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<ZeroPriceAcceptanceResult> AcceptZeroPriceAsync(ZeroPriceAcceptanceRequest request,
        CancellationToken ct) => throw new NotSupportedException();
}