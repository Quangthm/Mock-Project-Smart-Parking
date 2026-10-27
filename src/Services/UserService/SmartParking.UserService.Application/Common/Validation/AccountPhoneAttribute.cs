using System.ComponentModel.DataAnnotations;

namespace UserService.Application.Common.Validation;

public sealed class AccountPhoneAttribute : ValidationAttribute
{
    public AccountPhoneAttribute() => ErrorMessage = "Enter a valid phone number (9–15 digits, optionally starting with +).";
    public override bool IsValid(object? value) => value is null || value is string phone &&
        (string.IsNullOrWhiteSpace(phone) || !phone.Contains('@') && SignInContact.IsValid(phone));
}
