using FluentValidation;
using UserService.Application.Common.Validation;
namespace UserService.Application.Usecase.Login;
public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).Must(SignInContact.IsValid)
            .WithErrorCode("INVALID_CONTACT").WithMessage(SignInContact.Message);
        RuleFor(x => x.Password).NotEmpty()
            .WithErrorCode("PasswordIsRequired").WithMessage("Password is required.");
    }
}
