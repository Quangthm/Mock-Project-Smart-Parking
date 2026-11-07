using FluentValidation;

namespace SmartParking.OperationService.Application.Usecase.Structure.Commands.CompleteStructureEdit;

public sealed class CompleteStructureEditCommandValidator : AbstractValidator<CompleteStructureEditCommand>
{
    public CompleteStructureEditCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage("TenantId is required.");
        RuleFor(x => x.SiteId).NotEmpty().WithMessage("SiteId is required.");
        RuleFor(x => x.Token).NotEmpty().WithMessage("Token is required.");
    }
}
