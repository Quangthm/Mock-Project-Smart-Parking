using FluentValidation;
using SmartParking.OperationService.Application.Policies.Commands;

namespace SmartParking.OperationService.Application.Policies.Validators;

public sealed class SaveLotPolicyDraftCommandValidator : AbstractValidator<SaveLotPolicyDraftCommand>
{
    public SaveLotPolicyDraftCommandValidator()
    {
        RuleFor(x => x.SiteId)
            .NotEmpty()
            .WithMessage("siteId is required.");

        // Flat properties validation
        RuleFor(x => x.Request.PaymentHoldDurationMinutes)
            .InclusiveBetween(5, 30)
            .When(x => x.Request.PaymentHoldDurationMinutes.HasValue)
            .WithErrorCode("POLICY_OUT_OF_BOUNDS")
            .WithMessage("paymentHoldDurationMinutes must be between 5 and 30 minutes.");

        RuleFor(x => x.Request.ArrivalLateToleranceMinutes)
            .InclusiveBetween(15, 30)
            .When(x => x.Request.ArrivalLateToleranceMinutes.HasValue)
            .WithErrorCode("POLICY_OUT_OF_BOUNDS")
            .WithMessage("arrivalLateToleranceMinutes must be between 15 and 30 minutes.");

        RuleFor(x => x.Request.MinReservationDurationMinutes)
            .InclusiveBetween(15, 60)
            .When(x => x.Request.MinReservationDurationMinutes.HasValue)
            .WithErrorCode("POLICY_OUT_OF_BOUNDS")
            .WithMessage("minReservationDurationMinutes must be between 15 and 60 minutes.");

        RuleFor(x => x.Request.MaxReservationDurationHours)
            .InclusiveBetween(1, 168)
            .When(x => x.Request.MaxReservationDurationHours.HasValue)
            .WithErrorCode("POLICY_OUT_OF_BOUNDS")
            .WithMessage("maxReservationDurationHours must be between 1 and 168 hours.");

        RuleFor(x => x.Request.ReservationProtectionWindowHours)
            .InclusiveBetween(1, 12)
            .When(x => x.Request.ReservationProtectionWindowHours.HasValue)
            .WithErrorCode("POLICY_OUT_OF_BOUNDS")
            .WithMessage("reservationProtectionWindowHours must be between 1 and 12 hours.");

        RuleFor(x => x.Request.AllocationLeadTimeMinutes)
            .InclusiveBetween(15, 180)
            .When(x => x.Request.AllocationLeadTimeMinutes.HasValue)
            .WithErrorCode("POLICY_OUT_OF_BOUNDS")
            .WithMessage("allocationLeadTimeMinutes must be between 15 and 180 minutes.");

        RuleFor(x => x.Request.OccupancyValidityTimespanHours)
            .InclusiveBetween(1, 8)
            .When(x => x.Request.OccupancyValidityTimespanHours.HasValue)
            .WithErrorCode("POLICY_OUT_OF_BOUNDS")
            .WithMessage("occupancyValidityTimespanHours must be between 1 and 8 hours.");

        RuleFor(x => x.Request.MaxAdvanceBookingDays)
            .InclusiveBetween(1, 30)
            .When(x => x.Request.MaxAdvanceBookingDays.HasValue)
            .WithErrorCode("POLICY_OUT_OF_BOUNDS")
            .WithMessage("maxAdvanceBookingDays must be between 1 and 30 days.");

        RuleFor(x => x.Request.InterBookingBufferMinutes)
            .InclusiveBetween(0, 60)
            .When(x => x.Request.InterBookingBufferMinutes.HasValue)
            .WithErrorCode("POLICY_OUT_OF_BOUNDS")
            .WithMessage("interBookingBufferMinutes must be between 0 and 60 minutes.");

        RuleFor(x => x.Request.DriverReallocationRequestLimit)
            .InclusiveBetween(0, 5)
            .When(x => x.Request.DriverReallocationRequestLimit.HasValue)
            .WithErrorCode("POLICY_OUT_OF_BOUNDS")
            .WithMessage("driverReallocationRequestLimit must be between 0 and 5.");

        RuleFor(x => x.Request.FacilityFailureRefundLeadMinutes)
            .InclusiveBetween(15, 60)
            .When(x => x.Request.FacilityFailureRefundLeadMinutes.HasValue)
            .WithErrorCode("POLICY_OUT_OF_BOUNDS")
            .WithMessage("facilityFailureRefundLeadMinutes must be between 15 and 60 minutes.");

        // Nested policies envelope validation
        When(x => x.Request.Policies != null, () =>
        {
            RuleFor(x => x.Request.Policies!.PaymentHoldDurationMinutes)
                .InclusiveBetween(5, 30)
                .When(x => x.Request.Policies!.PaymentHoldDurationMinutes.HasValue)
                .WithErrorCode("POLICY_OUT_OF_BOUNDS")
                .WithMessage("paymentHoldDurationMinutes must be between 5 and 30 minutes.");

            RuleFor(x => x.Request.Policies!.ArrivalLateToleranceMinutes)
                .InclusiveBetween(15, 30)
                .When(x => x.Request.Policies!.ArrivalLateToleranceMinutes.HasValue)
                .WithErrorCode("POLICY_OUT_OF_BOUNDS")
                .WithMessage("arrivalLateToleranceMinutes must be between 15 and 30 minutes.");

            RuleFor(x => x.Request.Policies!.MinReservationDurationMinutes)
                .InclusiveBetween(15, 60)
                .When(x => x.Request.Policies!.MinReservationDurationMinutes.HasValue)
                .WithErrorCode("POLICY_OUT_OF_BOUNDS")
                .WithMessage("minReservationDurationMinutes must be between 15 and 60 minutes.");

            RuleFor(x => x.Request.Policies!.MaxReservationDurationHours)
                .InclusiveBetween(1, 168)
                .When(x => x.Request.Policies!.MaxReservationDurationHours.HasValue)
                .WithErrorCode("POLICY_OUT_OF_BOUNDS")
                .WithMessage("maxReservationDurationHours must be between 1 and 168 hours.");

            RuleFor(x => x.Request.Policies!.ReservationProtectionWindowHours)
                .InclusiveBetween(1, 12)
                .When(x => x.Request.Policies!.ReservationProtectionWindowHours.HasValue)
                .WithErrorCode("POLICY_OUT_OF_BOUNDS")
                .WithMessage("reservationProtectionWindowHours must be between 1 and 12 hours.");

            RuleFor(x => x.Request.Policies!.AllocationLeadTimeMinutes)
                .InclusiveBetween(15, 180)
                .When(x => x.Request.Policies!.AllocationLeadTimeMinutes.HasValue)
                .WithErrorCode("POLICY_OUT_OF_BOUNDS")
                .WithMessage("allocationLeadTimeMinutes must be between 15 and 180 minutes.");

            RuleFor(x => x.Request.Policies!.OccupancyValidityTimespanHours)
                .InclusiveBetween(1, 8)
                .When(x => x.Request.Policies!.OccupancyValidityTimespanHours.HasValue)
                .WithErrorCode("POLICY_OUT_OF_BOUNDS")
                .WithMessage("occupancyValidityTimespanHours must be between 1 and 8 hours.");

            RuleFor(x => x.Request.Policies!.MaxAdvanceBookingDays)
                .InclusiveBetween(1, 30)
                .When(x => x.Request.Policies!.MaxAdvanceBookingDays.HasValue)
                .WithErrorCode("POLICY_OUT_OF_BOUNDS")
                .WithMessage("maxAdvanceBookingDays must be between 1 and 30 days.");

            RuleFor(x => x.Request.Policies!.InterBookingBufferMinutes)
                .InclusiveBetween(0, 60)
                .When(x => x.Request.Policies!.InterBookingBufferMinutes.HasValue)
                .WithErrorCode("POLICY_OUT_OF_BOUNDS")
                .WithMessage("interBookingBufferMinutes must be between 0 and 60 minutes.");

            RuleFor(x => x.Request.Policies!.DriverReallocationRequestLimit)
                .InclusiveBetween(0, 5)
                .When(x => x.Request.Policies!.DriverReallocationRequestLimit.HasValue)
                .WithErrorCode("POLICY_OUT_OF_BOUNDS")
                .WithMessage("driverReallocationRequestLimit must be between 0 and 5.");

            RuleFor(x => x.Request.Policies!.FacilityFailureRefundLeadMinutes)
                .InclusiveBetween(15, 60)
                .When(x => x.Request.Policies!.FacilityFailureRefundLeadMinutes.HasValue)
                .WithErrorCode("POLICY_OUT_OF_BOUNDS")
                .WithMessage("facilityFailureRefundLeadMinutes must be between 15 and 60 minutes.");
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
