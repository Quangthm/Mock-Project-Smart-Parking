using FluentValidation;

namespace SmartParking.OperationService.Application.Reservations;

public interface IPolicyDraftInput
{
    int MinReservationDurationMinutes { get; }
    int MaxReservationDurationHours { get; }
    int PaymentHoldDurationMinutes { get; }
    int MaxAdvanceBookingDays { get; }
    int InterBookingBufferMinutes { get; }
    int ReservationProtectionWindowHours { get; }
    int AllocationLeadTimeMinutes { get; }
    int FacilityFailureRefundLeadMinutes { get; }
    int OccupancyValidityTimespanHours { get; }
    int ArrivalLateToleranceMinutes { get; }
    int RapidCancellationCount { get; }
    int RapidCancellationWindowMinutes { get; }
    int RapidCancellationRestrictionMinutes { get; }
    int DailyCancellationLimit { get; }
}

public static class PolicyDraftValidationExtensions
{
    public static void ApplyPolicyDraftRules<T>(this AbstractValidator<T> validator) where T : IPolicyDraftInput
    {
        // Business ranges are Admin-configured and enforced at publish/resolve time. Only the
        // technical limits that keep hour/day values representable as non-overflowing int minutes
        // are applied here (an hour column stores floor value; a day column stores value*1440).
        const int maxHourValue = int.MaxValue / 60;
        const int maxDayValue = int.MaxValue / 1440;

        validator.RuleFor(x => x.MinReservationDurationMinutes).GreaterThan(0)
            .WithMessage("Minimum reservation duration must be greater than zero.");
        validator.RuleFor(x => x.MaxReservationDurationHours).InclusiveBetween(1, maxHourValue)
            .WithMessage($"Maximum reservation duration must be between 1 and {maxHourValue} hours.");
        validator.RuleFor(x => x.MaxReservationDurationHours)
            .Must((cmd, hours) => (long)hours * 60 >= cmd.MinReservationDurationMinutes)
            .WithMessage("Maximum reservation duration must cover the minimum reservation duration.");
        validator.RuleFor(x => x.PaymentHoldDurationMinutes).GreaterThan(0)
            .WithMessage("Payment hold duration must be greater than zero.");
        validator.RuleFor(x => x.MaxAdvanceBookingDays).InclusiveBetween(0, maxDayValue)
            .WithMessage($"Maximum advance booking days must be between 0 and {maxDayValue}.");
        validator.RuleFor(x => x.InterBookingBufferMinutes).GreaterThanOrEqualTo(0)
            .WithMessage("Inter-booking buffer must not be negative.");
        validator.RuleFor(x => x.ReservationProtectionWindowHours).InclusiveBetween(0, maxHourValue)
            .WithMessage($"Reservation protection window must be between 0 and {maxHourValue} hours.");
        validator.RuleFor(x => x.AllocationLeadTimeMinutes).GreaterThanOrEqualTo(0)
            .WithMessage("Allocation lead time must not be negative.");
        validator.RuleFor(x => x.FacilityFailureRefundLeadMinutes).GreaterThanOrEqualTo(0)
            .WithMessage("Facility failure refund lead time must not be negative.");
        validator.RuleFor(x => x.OccupancyValidityTimespanHours).InclusiveBetween(0, maxHourValue)
            .WithMessage($"Occupancy validity timespan must be between 0 and {maxHourValue} hours.");
        validator.RuleFor(x => x.ArrivalLateToleranceMinutes).InclusiveBetween(15, 30)
            .WithMessage("Arrival late tolerance must be between 15 and 30 minutes.");
        validator.RuleFor(x => x.RapidCancellationCount).GreaterThan(0)
            .WithMessage("Rapid cancellation count must be greater than zero.");
        validator.RuleFor(x => x.RapidCancellationWindowMinutes).GreaterThan(0)
            .WithMessage("Rapid cancellation window must be greater than zero.");
        validator.RuleFor(x => x.RapidCancellationRestrictionMinutes).GreaterThan(0)
            .WithMessage("Rapid cancellation restriction duration must be greater than zero.");
        validator.RuleFor(x => x.DailyCancellationLimit).GreaterThan(0)
            .WithMessage("Daily cancellation limit must be greater than zero.");
    }
}
