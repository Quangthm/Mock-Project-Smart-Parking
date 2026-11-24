using FluentValidation;

namespace SmartParking.OperationService.Application.Usecase.Structure.Commands.ApplyMaintenanceWindow;

public sealed class ApplyMaintenanceWindowCommandValidator : AbstractValidator<ApplyMaintenanceWindowCommand>
{
    public ApplyMaintenanceWindowCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage("Tenant is required.");
        RuleFor(x => x.SiteId).NotEmpty().WithMessage("Site is required.");
        RuleFor(x => x.WindowId).NotEmpty().WithMessage("Maintenance window is required.");
        RuleFor(x => x.End).GreaterThan(x => x.Start).WithMessage("Maintenance window end must be after its start.");
        RuleFor(x => x.SlotId)
            .Must((command, slotId) => slotId is null || command.UnitId is not null)
            .WithMessage("A slot-scoped maintenance window requires a unit.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("A maintenance reason is required.")
            .MaximumLength(500).WithMessage("A maintenance reason cannot exceed 500 characters.");
    }
}