namespace SmartParking.OperationService.Application.Reservations;

// Serializes one worker workflow across short local transactions and remote calls.
// It is not a scarce-capacity/site lock and grants no business entitlement.
public interface IReservationWorkflowConcurrency
{
    Task<IAsyncDisposable?> TryEnterAsync(Guid workflowId, CancellationToken ct);
}
