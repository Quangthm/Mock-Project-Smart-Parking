using FluentValidation;

namespace SmartParking.NotificationAuditService.Tests.API.Probes;

public sealed class NotificationErrorProbeValidator : AbstractValidator<NotificationErrorProbeCommand>
{
    public NotificationErrorProbeValidator(NotificationErrorProbeEvents events)
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
