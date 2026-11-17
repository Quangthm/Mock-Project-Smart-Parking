using FluentValidation;
using SmartParking.OperationService.Application.Policies.Commands;

namespace SmartParking.OperationService.Application.Policies.Validators;

// Owner draft values are validated only against technical/sanity limits here. Business bounds are
// Admin-configured and enforced at publish time (LotPolicyRepository) and by the domain resolver,
// so a value such as 75 minutes must be accepted whenever Admin has approved a matching range.
public sealed class SaveLotPolicyDraftCommandValidator : AbstractValidator<SaveLotPolicyDraftCommand>
{
    private const int MaxMinuteValue = int.MaxValue;
    private const int MaxHourValue = int.MaxValue / 60;
    private const int MaxDayValue = int.MaxValue / 1440;

    public SaveLotPolicyDraftCommandValidator()
    {
        RuleFor(x => x.SiteId)
            .NotEmpty()
            .WithMessage("siteId is required.");

        // Flat properties validation
        RuleFor(x => x.Request.PaymentHoldDurationMinutes)
            .GreaterThan(0)
            .LessThanOrEqualTo(MaxMinuteValue)
            .When(x => x.Request.PaymentHoldDurationMinutes.HasValue)
            .WithErrorCode("POLICY_OUT_OF_BOUNDS")
            .WithMessage("paymentHoldDurationMinutes must be greater than 0.");

        RuleFor(x => x.Request.ArrivalLateToleranceMinutes)
            .InclusiveBetween(15, 30)
            .When(x => x.Request.ArrivalLateToleranceMinutes.HasValue)
            .WithErrorCode("POLICY_OUT_OF_BOUNDS")
            .WithMessage("arrivalLateToleranceMinutes must be between 15 and 30 minutes.");

        RuleFor(x => x.Request.MinReservationDurationMinutes)
            .GreaterThan(0)
            .LessThanOrEqualTo(MaxMinuteValue)
            .When(x => x.Request.MinReservationDurationMinutes.HasValue)
            .WithErrorCode("POLICY_OUT_OF_BOUNDS")
            .WithMessage("minReservationDurationMinutes must be greater than 0.");

        RuleFor(x => x.Request.MaxReservationDurationHours)
            .GreaterThan(0)
            .LessThanOrEqualTo(MaxHourValue)
            .When(x => x.Request.MaxReservationDurationHours.HasValue)
            .WithErrorCode("POLICY_OUT_OF_BOUNDS")
            .WithMessage($"maxReservationDurationHours must be between 1 and {MaxHourValue} hours.");

        RuleFor(x => x.Request.ReservationProtectionWindowHours)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(MaxHourValue)
            .When(x => x.Request.ReservationProtectionWindowHours.HasValue)
            .WithErrorCode("POLICY_OUT_OF_BOUNDS")
            .WithMessage($"reservationProtectionWindowHours must be between 0 and {MaxHourValue} hours.");

        RuleFor(x => x.Request.AllocationLeadTimeMinutes)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(MaxMinuteValue)
            .When(x => x.Request.AllocationLeadTimeMinutes.HasValue)
            .WithErrorCode("POLICY_OUT_OF_BOUNDS")
            .WithMessage("allocationLeadTimeMinutes must not be negative.");

        RuleFor(x => x.Request.OccupancyValidityTimespanHours)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(MaxHourValue)
            .When(x => x.Request.OccupancyValidityTimespanHours.HasValue)
            .WithErrorCode("POLICY_OUT_OF_BOUNDS")
            .WithMessage($"occupancyValidityTimespanHours must be between 0 and {MaxHourValue} hours.");

        RuleFor(x => x.Request.MaxAdvanceBookingDays)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(MaxDayValue)
            .When(x => x.Request.MaxAdvanceBookingDays.HasValue)
            .WithErrorCode("POLICY_OUT_OF_BOUNDS")
            .WithMessage($"maxAdvanceBookingDays must be between 0 and {MaxDayValue} days.");

        RuleFor(x => x.Request.InterBookingBufferMinutes)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(MaxMinuteValue)
            .When(x => x.Request.InterBookingBufferMinutes.HasValue)
            .WithErrorCode("POLICY_OUT_OF_BOUNDS")
            .WithMessage("interBookingBufferMinutes must not be negative.");

        RuleFor(x => x.Request.DriverReallocationRequestLimit)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(MaxMinuteValue)
            .When(x => x.Request.DriverReallocationRequestLimit.HasValue)
            .WithErrorCode("POLICY_OUT_OF_BOUNDS")
            .WithMessage("driverReallocationRequestLimit must not be negative.");

        RuleFor(x => x.Request.FacilityFailureRefundLeadMinutes)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(MaxMinuteValue)
            .When(x => x.Request.FacilityFailureRefundLeadMinutes.HasValue)
            .WithErrorCode("POLICY_OUT_OF_BOUNDS")
            .WithMessage("facilityFailureRefundLeadMinutes must not be negative.");

        // Nested policies envelope validation
        When(x => x.Request.Policies != null, () =>
        {
            RuleFor(x => x.Request.Policies!.PaymentHoldDurationMinutes)
                .GreaterThan(0)
                .LessThanOrEqualTo(MaxMinuteValue)
                .When(x => x.Request.Policies!.PaymentHoldDurationMinutes.HasValue)
                .WithErrorCode("POLICY_OUT_OF_BOUNDS")
                .WithMessage("paymentHoldDurationMinutes must be greater than 0.");

            RuleFor(x => x.Request.Policies!.ArrivalLateToleranceMinutes)
                .InclusiveBetween(15, 30)
                .When(x => x.Request.Policies!.ArrivalLateToleranceMinutes.HasValue)
                .WithErrorCode("POLICY_OUT_OF_BOUNDS")
                .WithMessage("arrivalLateToleranceMinutes must be between 15 and 30 minutes.");

            RuleFor(x => x.Request.Policies!.MinReservationDurationMinutes)
                .GreaterThan(0)
                .LessThanOrEqualTo(MaxMinuteValue)
                .When(x => x.Request.Policies!.MinReservationDurationMinutes.HasValue)
                .WithErrorCode("POLICY_OUT_OF_BOUNDS")
                .WithMessage("minReservationDurationMinutes must be greater than 0.");

            RuleFor(x => x.Request.Policies!.MaxReservationDurationHours)
                .GreaterThan(0)
                .LessThanOrEqualTo(MaxHourValue)
                .When(x => x.Request.Policies!.MaxReservationDurationHours.HasValue)
                .WithErrorCode("POLICY_OUT_OF_BOUNDS")
                .WithMessage($"maxReservationDurationHours must be between 1 and {MaxHourValue} hours.");

            RuleFor(x => x.Request.Policies!.ReservationProtectionWindowHours)
                .GreaterThanOrEqualTo(0)
                .LessThanOrEqualTo(MaxHourValue)
                .When(x => x.Request.Policies!.ReservationProtectionWindowHours.HasValue)
                .WithErrorCode("POLICY_OUT_OF_BOUNDS")
                .WithMessage($"reservationProtectionWindowHours must be between 0 and {MaxHourValue} hours.");

            RuleFor(x => x.Request.Policies!.AllocationLeadTimeMinutes)
                .GreaterThanOrEqualTo(0)
                .LessThanOrEqualTo(MaxMinuteValue)
                .When(x => x.Request.Policies!.AllocationLeadTimeMinutes.HasValue)
                .WithErrorCode("POLICY_OUT_OF_BOUNDS")
                .WithMessage("allocationLeadTimeMinutes must not be negative.");

            RuleFor(x => x.Request.Policies!.OccupancyValidityTimespanHours)
                .GreaterThanOrEqualTo(0)
                .LessThanOrEqualTo(MaxHourValue)
                .When(x => x.Request.Policies!.OccupancyValidityTimespanHours.HasValue)
                .WithErrorCode("POLICY_OUT_OF_BOUNDS")
                .WithMessage($"occupancyValidityTimespanHours must be between 0 and {MaxHourValue} hours.");

            RuleFor(x => x.Request.Policies!.MaxAdvanceBookingDays)
                .GreaterThanOrEqualTo(0)
                .LessThanOrEqualTo(MaxDayValue)
                .When(x => x.Request.Policies!.MaxAdvanceBookingDays.HasValue)
                .WithErrorCode("POLICY_OUT_OF_BOUNDS")
                .WithMessage($"maxAdvanceBookingDays must be between 0 and {MaxDayValue} days.");

            RuleFor(x => x.Request.Policies!.InterBookingBufferMinutes)
                .GreaterThanOrEqualTo(0)
                .LessThanOrEqualTo(MaxMinuteValue)
                .When(x => x.Request.Policies!.InterBookingBufferMinutes.HasValue)
                .WithErrorCode("POLICY_OUT_OF_BOUNDS")
                .WithMessage("interBookingBufferMinutes must not be negative.");

            RuleFor(x => x.Request.Policies!.DriverReallocationRequestLimit)
                .GreaterThanOrEqualTo(0)
                .LessThanOrEqualTo(MaxMinuteValue)
                .When(x => x.Request.Policies!.DriverReallocationRequestLimit.HasValue)
                .WithErrorCode("POLICY_OUT_OF_BOUNDS")
                .WithMessage("driverReallocationRequestLimit must not be negative.");

            RuleFor(x => x.Request.Policies!.FacilityFailureRefundLeadMinutes)
                .GreaterThanOrEqualTo(0)
                .LessThanOrEqualTo(MaxMinuteValue)
                .When(x => x.Request.Policies!.FacilityFailureRefundLeadMinutes.HasValue)
                .WithErrorCode("POLICY_OUT_OF_BOUNDS")
                .WithMessage("facilityFailureRefundLeadMinutes must not be negative.");
        });

        // Time logic: min <= max
        RuleFor(x => x)
            .Must(x =>
            {
                var cfg = x.Request.ToDraftConfig();
                return !cfg.MinReservationDurationMinutes.HasValue ||
                       !cfg.MaxReservationDurationHours.HasValue ||
                       cfg.MinReservationDurationMinutes.Value <= cfg.MaxReservationDurationHours.Value * 60;
            })
            .WithErrorCode("INVALID_TIME_DURATION")
            .WithMessage("minReservationDurationMinutes cannot exceed maxReservationDurationHours.");

        RuleFor(x => x.Request.ScheduledActivationAt)
            .Must(at => at > DateTimeOffset.UtcNow)
            .When(x => x.Request.ScheduledActivationAt.HasValue)
            .WithErrorCode("INVALID_ACTIVATION_SCHEDULE")
            .WithMessage("scheduledActivationAt must be a future timestamp.");
    }
}
