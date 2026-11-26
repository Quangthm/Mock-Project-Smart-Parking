namespace SmartParking.OperationService.Application.Common.Interfaces.Services;

public interface IRealtimeCheckoutNotifier
{
    Task CheckoutCompletedAsync(
        Guid siteId, Guid sessionId, Guid? reservationId, Guid paymentOrderId, CancellationToken ct);
}
