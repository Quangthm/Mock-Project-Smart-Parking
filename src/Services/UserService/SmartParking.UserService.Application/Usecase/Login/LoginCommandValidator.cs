using FluentValidation;

namespace UserService.Application.Usecase.Login;

public class LoginCommandValidator
    : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .WithErrorCode("EmailIsRequired")
            .WithMessage("Email is required.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithErrorCode("PasswordIsRequired")
            .WithMessage("Password is required.");
    }
}