using FluentValidation;

namespace SmartParking.FacilityService.Application.Usecases.Internal.Commands.ProvisionOwner;

public sealed class ProvisionOwnerCommandValidator : AbstractValidator<ProvisionOwnerCommand>
{
    public ProvisionOwnerCommandValidator()
    {
        RuleFor(x => x.ApplicationId).NotEmpty();
        RuleFor(x => x.Body).NotNull();
    }
}
