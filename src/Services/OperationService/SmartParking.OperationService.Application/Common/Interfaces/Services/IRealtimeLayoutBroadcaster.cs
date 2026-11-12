namespace SmartParking.OperationService.Application.Common.Interfaces.Services;

// Broadcasts realtime occupancy deltas to operator SignalR clients (group site:{siteId}:operators).
// Implemented in the API layer (needs IHubContext<ParkingLotLayoutHub>); the outbox worker
// resolves it from DI and skips broadcasting when no implementation is registered.
public interface IRealtimeLayoutBroadcaster
{
    Task BroadcastAsync(OutboxMessage message, CancellationToken cancellationToken);
}
