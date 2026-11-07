using FluentValidation;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.MarkBackup;

public sealed class MarkBackupCommandValidator : AbstractValidator<MarkBackupCommand>
{
    public MarkBackupCommandValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty().WithMessage("SiteId is required.");
        RuleFor(x => x.SlotId).NotEmpty().WithMessage("SlotId is required.");
    }
}
