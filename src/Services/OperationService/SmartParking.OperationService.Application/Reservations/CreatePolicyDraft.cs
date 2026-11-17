using FluentValidation;
using MediatR;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record CreatePolicyDraftCommand(
    Guid? TenantId,
    Guid? SiteId,
    int PaymentHoldDurationMinutes,
    int MinReservationDurationMinutes,
    int MaxReservationDurationHours,
    int MaxAdvanceBookingDays,
    int InterBookingBufferMinutes,
    int ReservationProtectionWindowHours,
    int AllocationLeadTimeMinutes,
    int FacilityFailureRefundLeadMinutes,
    int OccupancyValidityTimespanHours,
    int ArrivalLateToleranceMinutes,
    int RapidCancellationCount,
    int RapidCancellationWindowMinutes,
    int RapidCancellationRestrictionMinutes,
    int DailyCancellationLimit,
    bool? ArrivalLateToleranceEnabled,
    string CancellationTimezone,
    IReadOnlyDictionary<string, PolicyBoundDto>? OverrideBounds) : IRequest<CreatePolicyDraftResultDto>, IPolicyDraftInput;

public sealed class CreatePolicyDraftHandler(IReservationPolicyRepository policies, TimeProvider clock)
    : IRequestHandler<CreatePolicyDraftCommand, CreatePolicyDraftResultDto>
{
    public async Task<CreatePolicyDraftResultDto> Handle(CreatePolicyDraftCommand command, CancellationToken ct)
    {
        if (command.TenantId is not null || command.SiteId is not null)
            throw new ForbiddenScopeException(
                "Platform administrators may only create platform-scope policy drafts.");
        PolicyBoundsValidator.Validate(command.OverrideBounds);

        var now = clock.GetUtcNow().ToMicroseconds();
        var row = new OperationalPolicyRow
        {
            Id = Guid.NewGuid(),
            TenantId = null,
            SiteId = null,
            Version = await policies.GetNextPlatformVersionAsync(ct),
            CreatedAt = now,
            UpdatedAt = now,
            EffectiveFrom = now,
            EffectiveUntil = null,
            IsActive = false,
            ArrivalLateToleranceEnabled = null
        };
        AdminPolicyWriteMapping.ApplyPlatformValues(row, command.PaymentHoldDurationMinutes,
            command.MinReservationDurationMinutes, command.MaxReservationDurationHours, command.MaxAdvanceBookingDays,
            command.InterBookingBufferMinutes, command.ReservationProtectionWindowHours,
            command.AllocationLeadTimeMinutes, command.FacilityFailureRefundLeadMinutes,
            command.OccupancyValidityTimespanHours, command.ArrivalLateToleranceMinutes,
            command.RapidCancellationCount, command.RapidCancellationWindowMinutes,
            command.RapidCancellationRestrictionMinutes, command.DailyCancellationLimit,
            command.CancellationTimezone, command.OverrideBounds);
        await policies.CreateDraftAsync(row, ct);
        return new CreatePolicyDraftResultDto(row.Id, row.Version, "PLATFORM", "DRAFT", false, row.CreatedAt,
            row.UpdatedAt);
    }
}

public sealed class CreatePolicyDraftCommandValidator : AbstractValidator<CreatePolicyDraftCommand>
{
    public CreatePolicyDraftCommandValidator()
    {
        this.ApplyPolicyDraftRules();
        RuleFor(x => x.CancellationTimezone).Equal("Asia/Ho_Chi_Minh")
            .WithMessage("Cancellation timezone must be Asia/Ho_Chi_Minh.");
    }
}