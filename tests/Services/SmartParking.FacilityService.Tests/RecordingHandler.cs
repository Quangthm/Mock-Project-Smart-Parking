using MediatR;

namespace SmartParking.FacilityService.Tests;

internal sealed class RecordingHandler<TCommand> : IRequestHandler<TCommand>
    where TCommand : IRequest
{
    public List<object> Calls { get; } = [];

    public Task Handle(TCommand request, CancellationToken cancellationToken)
    {
        Calls.Add(request!);
        return Task.CompletedTask;
    }
}
