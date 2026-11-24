using FluentValidation;

namespace SmartParking.OperationService.Application.Usecase.Structure.Commands.CancelMaintenanceWindow;

public sealed class CancelMaintenanceWindowCommandValidator : AbstractValidator<CancelMaintenanceWindowCommand>
{
    public CancelMaintenanceWindowCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage("Tenant is required.");
        RuleFor(x => x.SiteId).NotEmpty().WithMessage("Site is required.");
        RuleFor(x => x.WindowId).NotEmpty().WithMessage("Maintenance window is required.");
    }
}