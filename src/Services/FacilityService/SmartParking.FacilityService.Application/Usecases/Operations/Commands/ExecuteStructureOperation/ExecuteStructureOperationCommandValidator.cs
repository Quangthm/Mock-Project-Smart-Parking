using FluentValidation;

namespace SmartParking.FacilityService.Application.Usecases.Operations.Commands.ExecuteStructureOperation;

public sealed class ExecuteStructureOperationCommandValidator : AbstractValidator<ExecuteStructureOperationCommand>
{
    public ExecuteStructureOperationCommandValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty();
        RuleFor(x => x.IdempotencyKey).NotEmpty();
        RuleFor(x => x.Edit).NotNull();
    }
}
