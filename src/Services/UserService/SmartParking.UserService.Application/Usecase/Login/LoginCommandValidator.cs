using FluentValidation;

namespace UserService.Application.Usecase.Login;

public class LoginCommandValidator
    : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .Must(value => value != null && (System.Text.RegularExpressions.Regex.IsMatch(value.Trim(), @"^\+?[0-9]{9,15}$")
                || new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(value.Trim())))
            .WithErrorCode("EmailIsRequired")
            .WithMessage("A valid email or phone number is required.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithErrorCode("PasswordIsRequired")
            .WithMessage("Password is required.");
    }
}
