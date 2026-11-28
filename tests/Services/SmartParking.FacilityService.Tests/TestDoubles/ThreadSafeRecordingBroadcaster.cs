using SmartParking.OperationService.Application.Common.Interfaces.Services;
using OpsOutboxMessage = SmartParking.OperationService.Application.Common.Interfaces.Services.OutboxMessage;

namespace SmartParking.FacilityService.Tests.TestDoubles;

internal sealed class ThreadSafeRecordingBroadcaster(List<OpsOutboxMessage> target) : IRealtimeLayoutBroadcaster
{
    private readonly Lock _gate = new();

    public Task BroadcastAsync(OpsOutboxMessage message, CancellationToken cancellationToken)
    {
        lock (_gate) target.Add(message);
        return Task.CompletedTask;
    }
}
