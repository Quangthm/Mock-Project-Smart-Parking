using SmartParking.OperationService.Application.Common.Interfaces.Services;

namespace SmartParking.OperationService.Tests.TestDoubles;

internal sealed class RecordingLayoutBroadcaster(List<OutboxMessage> target) : IRealtimeLayoutBroadcaster
{
    public Task BroadcastAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        target.Add(message);
        return Task.CompletedTask;
    }
}
