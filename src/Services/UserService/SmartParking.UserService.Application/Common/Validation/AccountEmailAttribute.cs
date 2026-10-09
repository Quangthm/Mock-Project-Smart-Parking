using System.ComponentModel.DataAnnotations;

namespace UserService.Application.Common.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class AccountEmailAttribute : ValidationAttribute
{
    public AccountEmailAttribute() : base("Enter a valid email address.") { }
    // Required is enforced separately; Driver registration also supports phone-only accounts.
    public override bool IsValid(object? value) => value is null || value is string email &&
        (email.Length == 0 || SignInContact.IsValidEmail(email));
}
