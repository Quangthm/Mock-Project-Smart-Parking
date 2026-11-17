using FluentValidation;
using MediatR;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record SchedulePolicyRevisionCommand(Guid Id, DateTimeOffset? EffectiveFrom)
    : IRequest<PolicyActivationResultDto>;

public sealed class SchedulePolicyRevisionHandler(IReservationPolicyRepository policies, TimeProvider clock)
    : IRequestHandler<SchedulePolicyRevisionCommand, PolicyActivationResultDto>
{
    public async Task<PolicyActivationResultDto> Handle(SchedulePolicyRevisionCommand command, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var effectiveFrom = command.EffectiveFrom ?? now;
        if (effectiveFrom < now)
            throw new RequestValidationFailedException("Effective start timestamp cannot be in the past.");

        var outcome = await policies.ActivateRevisionAsync(command.Id, effectiveFrom, ct);
        var target = outcome.Target;
        return new PolicyActivationResultDto(target.Id, target.Version, "PLATFORM",
            effectiveFrom > now ? "SCHEDULED" : "CURRENT", target.IsActive, target.EffectiveFrom,
            target.EffectiveUntil,
            outcome.Superseded is { } predecessor
                ? new SupersededPolicyReferenceDto(predecessor.Id, predecessor.Version,
                    predecessor.EffectiveUntil ?? effectiveFrom)
                : null);
    }
}

public sealed class SchedulePolicyRevisionCommandValidator : AbstractValidator<SchedulePolicyRevisionCommand>
{
    public SchedulePolicyRevisionCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}