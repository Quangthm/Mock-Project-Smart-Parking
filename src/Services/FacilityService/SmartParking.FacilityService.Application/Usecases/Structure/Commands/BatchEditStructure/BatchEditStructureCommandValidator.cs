using FluentValidation;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.BatchEditStructure;

public sealed class BatchEditStructureCommandValidator : AbstractValidator<BatchEditStructureCommand>
{
    public BatchEditStructureCommandValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty().WithMessage("SiteId is required.");
        RuleFor(x => x.IdempotencyKey).NotEmpty().WithMessage("IdempotencyKey is required.");
        RuleFor(x => x.Items)
            .NotNull().WithMessage("A batch requires at least one item.")
            .Must(items => items is not null && items.Count >= 1 && items.Count <= 50)
            .WithMessage("A batch must contain 1-50 items.");
    }
}
