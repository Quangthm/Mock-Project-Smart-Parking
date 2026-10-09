using SmartParking.UserService.Domain.Enum;
namespace UserService.Application.Common.Models.Exceptions;
public static class SignInErrors
{
    public static AuthException ForStatus(UserStatus status) => ForCode(status switch
    {
        UserStatus.PendingApproval => "ACCOUNT_PENDING_APPROVAL",
        UserStatus.PendingVerification => "ACCOUNT_PENDING_VERIFICATION",
        UserStatus.Rejected => "ACCOUNT_REJECTED",
        UserStatus.Inactive => "ACCOUNT_INACTIVE",
        UserStatus.Locked => "ACCOUNT_LOCKED",
        _ => "ACCOUNT_ACCESS_DENIED"
    });
    public static AuthException ForCode(string? code) => new(code ?? "AUTH_FAILED", code switch
    {
        "ACCOUNT_PENDING_APPROVAL" => "Your account has not been approved yet. Please wait for administrator approval before signing in.",
        "ACCOUNT_PENDING_VERIFICATION" => "Your account has not been verified yet. Please complete contact verification before signing in.",
        "ACCOUNT_REJECTED" => "Your account application was rejected. Please contact the administrator.",
        "ACCOUNT_INACTIVE" => "Your account is inactive. Please contact the administrator to restore access.",
        "ACCOUNT_ACCESS_DENIED" => "Your account has not been granted access. Please contact the administrator.",
        "ACCOUNT_LOCKED" => "Account is locked.",
        "MFA_REQUIRED" => "Use OTP sign-in with your authenticator code.",
        _ => "Invalid email or password."
    }, code is "ACCOUNT_PENDING_APPROVAL" or "ACCOUNT_PENDING_VERIFICATION" or "ACCOUNT_REJECTED" or
        "ACCOUNT_INACTIVE" or "ACCOUNT_ACCESS_DENIED" or "ACCOUNT_LOCKED" ? 403 : 401);
}
