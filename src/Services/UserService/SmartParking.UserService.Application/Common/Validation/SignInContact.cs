using System.Text.RegularExpressions;
namespace UserService.Application.Common.Validation;
public static class SignInContact
{
    public const string Message = "Enter a valid email address or phone number (9–15 digits, optionally starting with +).";
    public static bool IsValid(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;
        var value = input.Trim();
        if (value.Length > 255 || value.Any(char.IsWhiteSpace)) return false;
        if (Regex.IsMatch(value, @"^\+?[0-9]{9,15}$")) return true;
        return IsValidEmail(value);
    }
    public static bool IsValidEmail(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;
        var value = input.Trim();
        if (value.Length > 255 || value.Any(char.IsWhiteSpace)) return false;
        var parts = value.Split('@');
        if (parts.Length != 2) return false;
        var local = parts[0]; var domain = parts[1];
        if (local.Length is < 1 or > 64 || local.StartsWith('.') || local.EndsWith('.') || local.Contains("..") ||
            !Regex.IsMatch(local, @"^[A-Za-z0-9.!#$%&'*+/=?^_`{|}~-]+$")) return false;
        var labels = domain.Split('.');
        return labels.Length >= 2 && labels.All(label => label.Length is >= 1 and <= 63 &&
            Regex.IsMatch(label, @"^[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?$"));
    }
}
