using FluentValidation;
using MediatR;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record UpdatePolicyDraftCommand(
    Guid Id,
    DateTimeOffset ExpectedUpdatedAt,
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
    IReadOnlyDictionary<string, PolicyBoundDto>? OverrideBounds) : IRequest<UpdatePolicyDraftResultDto>, IPolicyDraftInput;

public sealed class UpdatePolicyDraftHandler(IReservationPolicyRepository policies)
    : IRequestHandler<UpdatePolicyDraftCommand, UpdatePolicyDraftResultDto>
{
    public async Task<UpdatePolicyDraftResultDto> Handle(UpdatePolicyDraftCommand command, CancellationToken ct)
    {
        var target = await policies.GetRevisionByIdAsync(command.Id, ct)
            ?? throw new PolicyNotFoundException("The requested policy revision does not exist.");

        if (command.TenantId is not null || command.SiteId is not null || target.TenantId is not null ||
            target.SiteId is not null)
            throw new ForbiddenScopeException(
                "Platform administrators may only modify platform-scope policy drafts.");
        if (target.IsActive)
            throw new CannotModifyActivePolicyException(
                "This policy revision has already been activated and cannot be modified.");
        if (target.UpdatedAt != command.ExpectedUpdatedAt)
            throw new ResourceVersionConflictException(
                "The draft was modified by another session. Please reload and review the current draft before saving.");

        PolicyBoundsValidator.Validate(command.OverrideBounds);

        var row = new OperationalPolicyRow
        {
            Id = command.Id,
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

        var updatedAt = await policies.UpdateDraftOptimisticAsync(command.Id, row, command.ExpectedUpdatedAt, ct);
        return new UpdatePolicyDraftResultDto(target.Id, target.Version, "PLATFORM", "DRAFT", false, updatedAt.ToMicroseconds());
    }
}

public sealed class UpdatePolicyDraftCommandValidator : AbstractValidator<UpdatePolicyDraftCommand>
{
    public UpdatePolicyDraftCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        this.ApplyPolicyDraftRules();
        RuleFor(x => x.CancellationTimezone).Equal("Asia/Ho_Chi_Minh")
            .WithMessage("Cancellation timezone must be Asia/Ho_Chi_Minh.");
    }
}