using FluentValidation;
using SmartParking.UserService.Domain.ValueObjects;

namespace UserService.Application.Profiles.Commands;

/// <summary>Enforces the display-name rules on the own-profile update.</summary>
public sealed class UpdateOwnProfileValidator : AbstractValidator<UpdateOwnProfileCommand>
{
    public UpdateOwnProfileValidator()
    {
        RuleFor(x => x.FullName)
            .Must(ProfileName.IsValid)
            .WithErrorCode("INVALID_FULL_NAME")
            .WithMessage($"Enter a display name of 1 to {ProfileName.MaxLength} characters without control characters.");
    }
}
