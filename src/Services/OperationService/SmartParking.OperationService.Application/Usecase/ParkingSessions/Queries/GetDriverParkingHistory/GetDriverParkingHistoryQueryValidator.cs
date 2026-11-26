using FluentValidation;

namespace SmartParking.OperationService.Application.Usecase.ParkingSessions.Queries.GetDriverParkingHistory;

public sealed class GetDriverParkingHistoryQueryValidator : AbstractValidator<GetDriverParkingHistoryQuery>
{
    private static readonly string[] AllowedStatuses = ["ALL", "ACTIVE", "COMPLETED", "PENDING_PAYMENT"];

    public GetDriverParkingHistoryQueryValidator()
    {
        RuleFor(q => q.AccountId)
            .NotEmpty()
            .WithMessage("AccountId must not be empty.");

        RuleFor(q => q.Limit)
            .InclusiveBetween(1, 100)
            .WithMessage("Limit must be between 1 and 100.");

        RuleFor(q => q.StatusFilter)
            .Must(status => string.IsNullOrWhiteSpace(status) || AllowedStatuses.Contains(status.Trim().ToUpperInvariant()))
            .WithMessage("StatusFilter must be one of: ALL, ACTIVE, COMPLETED, PENDING_PAYMENT.");
    }
}

