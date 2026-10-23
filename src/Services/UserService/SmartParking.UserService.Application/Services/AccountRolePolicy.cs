using SmartParking.UserService.Domain.Entities;
namespace UserService.Application.Services;
public static class AccountRolePolicy
{
    public static string? CurrentRole(User user)
    {
        var code = user.Accounts.FirstOrDefault(a => a.Status == "ACTIVE" && a.DeletedOn == null && a.AccountRoles.Any())?
            .AccountRoles.First().RoleCode;
        return code?.ToUpperInvariant() switch
        {
            "PLATFORM_ADMIN" or "ADMIN" => "admin",
            "BUSINESS_OWNER" or "OWNER" => "owner",
            "SITE_OPERATOR" or "OPERATOR" when user.EmailVerifiedAt is not null || user.PhoneVerifiedAt is not null => "operator",
            "DRIVER" => "driver",
            _ => null
        };
    }

}
