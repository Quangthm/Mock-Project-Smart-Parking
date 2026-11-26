using MediatR;

namespace SmartParking.NotificationAuditService.Tests.API.Probes;

public sealed class NotificationErrorProbeHandler(NotificationErrorProbeEvents events)
    : IRequestHandler<NotificationErrorProbeCommand, string>
{
    public Task<string> Handle(NotificationErrorProbeCommand request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref events.HandledInputs);
        return Task.FromResult(request.Name!);
    }
}
