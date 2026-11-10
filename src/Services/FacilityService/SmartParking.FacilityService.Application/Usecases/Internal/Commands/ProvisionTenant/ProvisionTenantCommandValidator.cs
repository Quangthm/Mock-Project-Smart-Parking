using FluentValidation;

namespace SmartParking.FacilityService.Application.Usecases.Internal.Commands.ProvisionTenant;

public sealed class ProvisionTenantCommandValidator : AbstractValidator<ProvisionTenantCommand>
{
    public ProvisionTenantCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Body).NotNull();
    }
}
