using SmartParking.UserService.Domain.Entities;
using UserService.Application.Authorization;

namespace UserService.Application.Services;

public static class AccountRolePolicy
{
    public static Account? SelectLoginAccount(User user) => user.Accounts
        .Where(a => a.Id is not null && a.UserId == user.Id && a.Status == "ACTIVE" &&
            a.DeletedOn is null && CurrentRole(a) is not null &&
            (CurrentRole(a) != "operator" || user.EmailVerifiedAt is not null || user.PhoneVerifiedAt is not null))
        .OrderBy(a => a.Id).FirstOrDefault();

    public static string? CurrentRole(User user) =>
        SelectLoginAccount(user) is { } account ? CurrentRole(account) : null;

    public static string? CurrentRole(Account account) => AccessRoles.Map(account.RoleCode);
}
