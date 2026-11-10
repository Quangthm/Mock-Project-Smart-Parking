using FluentValidation;

namespace SmartParking.FacilityService.Application.Usecases.Gate.Commands.ReleaseOperationalBackup;

public sealed class ReleaseOperationalBackupCommandValidator : AbstractValidator<ReleaseOperationalBackupCommand>
{
    public ReleaseOperationalBackupCommandValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty();
        RuleFor(x => x.SlotId).NotEmpty();
    }
}
