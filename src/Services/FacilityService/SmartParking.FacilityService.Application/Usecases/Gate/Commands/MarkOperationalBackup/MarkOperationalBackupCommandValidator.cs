using FluentValidation;

namespace SmartParking.FacilityService.Application.Usecases.Gate.Commands.MarkOperationalBackup;

public sealed class MarkOperationalBackupCommandValidator : AbstractValidator<MarkOperationalBackupCommand>
{
    public MarkOperationalBackupCommandValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty();
        RuleFor(x => x.SlotId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty();
    }
}
