using FluentValidation;

namespace SmartParking.PaymentService.Tests.API.Probes;

public sealed class PaymentErrorProbeValidator : AbstractValidator<PaymentErrorProbeCommand>
{
    public PaymentErrorProbeValidator(PaymentErrorProbeEvents events)
    {
        RuleFor(request => request.Name).MustAsync(async (name, cancellationToken) =>
        {
            Interlocked.Increment(ref events.ValidatedInputs);
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            return !string.IsNullOrWhiteSpace(name);
        }).WithMessage("Name is required.");
    }
}
