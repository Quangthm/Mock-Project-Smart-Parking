using MediatR;

namespace SmartParking.PaymentService.Tests.API.Probes;

public sealed class PaymentErrorProbeHandler(PaymentErrorProbeEvents events)
    : IRequestHandler<PaymentErrorProbeCommand, string>
{
    public Task<string> Handle(PaymentErrorProbeCommand request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref events.HandledInputs);
        return Task.FromResult(request.Name!);
    }
}
