using SmartParking.UserService.Domain.Enum;

namespace SmartParking.UserService.Domain.Entities;

public static class OperatorActivation
{
    public static bool CanReissueInvitation(User user, IReadOnlyCollection<OperatorGrant> grants,
        Guid ownerId, IReadOnlyCollection<Guid> tenantIds)
    {
        if (user.DeletedOn is not null || user.EmailVerifiedAt is not null || user.PhoneVerifiedAt is not null ||
            user.Status is not (UserStatus.PendingVerification or UserStatus.Active)) return false;
        var accounts = user.Accounts.Where(account => account.DeletedOn is null).ToArray();
        return accounts.Length > 0 && grants.Count > 0 && grants.All(grant => grant.CreatedBy == ownerId) &&
            accounts.All(account => account.TenantId is not null && tenantIds.Contains(account.TenantId.Value) &&
                account.Status is "PENDING_VERIFICATION" or "ACTIVE" && account.AccountRoles.Count > 0 &&
                account.AccountRoles.All(role => role.RoleCode == "SITE_OPERATOR"));
    }

    public static void PrepareInvitationReissue(User user, IReadOnlyCollection<OperatorGrant> grants,
        Guid ownerId, IReadOnlyCollection<Guid> tenantIds)
    {
        if (!CanReissueInvitation(user, grants, ownerId, tenantIds))
            throw new InvalidOperationException("Operator is not eligible for invitation reissue.");
        // Legacy unverified Operators may be active after an administrative unlock.
        user.Status = UserStatus.PendingVerification;
        foreach (var account in user.Accounts.Where(account => account.DeletedOn is null))
            account.Status = "PENDING_VERIFICATION";
    }

    public static bool NeedsContactVerification(User user) => user.EmailVerifiedAt is null && user.PhoneVerifiedAt is null &&
        user.Accounts.Any(a => a.DeletedOn is null && a.AccountRoles.Any(r => r.RoleCode == "SITE_OPERATOR")) &&
        !user.Accounts.Any(a => a.DeletedOn is null && a.AccountRoles.Any(r => r.RoleCode != "SITE_OPERATOR"));

    public static void RequireVerification(User user)
    {
        if (!NeedsContactVerification(user)) return;
        user.Status = UserStatus.PendingVerification;
        foreach (var account in user.Accounts.Where(a => a.DeletedOn is null && a.Status == "ACTIVE" &&
                     a.AccountRoles.Any(r => r.RoleCode == "SITE_OPERATOR"))) account.Status = "PENDING_VERIFICATION";
    }
    public static bool CanActivate(User user, AuthChallenge challenge, DateTimeOffset now) =>
        user.DeletedOn is null && user.Status == UserStatus.PendingVerification &&
        challenge.UserId == user.Id && challenge.Purpose == "BOOTSTRAP_PASSWORD" &&
        challenge.Channel == "email" && string.Equals(challenge.Destination, user.Email, StringComparison.OrdinalIgnoreCase) &&
        challenge.ConsumedAt is null && challenge.ExpiresAt > now &&
        user.Accounts.Any(a => a.DeletedOn is null && a.Status == "PENDING_VERIFICATION" &&
            a.AccountRoles.Any(r => r.RoleCode == "SITE_OPERATOR"));

    public static void Activate(User user, AuthChallenge challenge, string passwordHash, DateTimeOffset now)
    {
        if (!CanActivate(user, challenge, now)) throw new InvalidOperationException("Operator invitation is not eligible for activation.");
        user.PasswordHash = passwordHash;
        user.EmailVerifiedAt = now;
        user.Status = UserStatus.Active;
        user.ModifiedOn = now;
        user.AccessVersion++;
        foreach (var account in user.Accounts.Where(a => a.DeletedOn is null && a.Status == "PENDING_VERIFICATION" &&
                     a.AccountRoles.Any(r => r.RoleCode == "SITE_OPERATOR"))) account.Status = "ACTIVE";
        challenge.ConsumedAt = now;
        challenge.CodeHash = "";
    }
}
