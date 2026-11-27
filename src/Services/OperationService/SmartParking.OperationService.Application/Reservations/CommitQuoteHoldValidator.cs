using FluentValidation;
namespace SmartParking.OperationService.Application.Reservations;

public sealed class CommitQuoteHoldValidator : AbstractValidator<CommitQuoteHoldCommand>
{
    public CommitQuoteHoldValidator() => RuleFor(x => x.RequestId).NotEmpty();
}
