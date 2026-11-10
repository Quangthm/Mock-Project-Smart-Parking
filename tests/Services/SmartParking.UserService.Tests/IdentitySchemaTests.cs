using Microsoft.EntityFrameworkCore;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Services;

namespace SmartParking.UserService.Tests;

public sealed class IdentitySchemaTests
{
    [PostgresFact, Trait("Category", "Postgres")]
    public async Task CanonicalSchemaPersistsActiveAndPendingOperatorStates()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        var verifiedAt = new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
        var active = Operator("active@example.com", UserStatus.Active, "ACTIVE", verifiedAt);
        var pending = Operator("pending@example.com", UserStatus.PendingVerification, "PENDING_VERIFICATION", null);
        await fixture.SeedTenantsAsync(active.Accounts.Single().TenantId!.Value, pending.Accounts.Single().TenantId!.Value);

        await using (var db = fixture.Open())
        {
            db.Users.AddRange(active, pending);
            await db.SaveChangesAsync();
        }

        await using var verification = fixture.Open();
        var users = await verification.Users.AsNoTracking().Include(user => user.Accounts)
            .ToDictionaryAsync(user => user.Id!.Value);
        var retained = users[active.Id!.Value];
        Assert.Equal(UserStatus.Active, retained.Status);
        Assert.Equal(verifiedAt, retained.PhoneVerifiedAt);
        Assert.All(retained.Accounts, account => Assert.Equal("ACTIVE", account.Status));
        Assert.Equal("operator", AccountRolePolicy.CurrentRole(retained));

        var awaitingVerification = users[pending.Id!.Value];
        Assert.Equal(UserStatus.PendingVerification, awaitingVerification.Status);
        Assert.Null(awaitingVerification.PhoneVerifiedAt);
        Assert.All(awaitingVerification.Accounts, account => Assert.Equal("PENDING_VERIFICATION", account.Status));
        Assert.Null(AccountRolePolicy.CurrentRole(awaitingVerification));
    }

    private static User Operator(string email, UserStatus status, string accountStatus, DateTimeOffset? phoneVerifiedAt)
    {
        var user = new User
        {
            Id = Guid.NewGuid(), Email = email, Phone = "+84912345678", FullName = "Schema Test Operator",
            PasswordHash = "unused-schema-test-hash", Status = status, PhoneVerifiedAt = phoneVerifiedAt
        };
        var account = new Account
        {
            Id = Guid.NewGuid(), User = user, UserId = user.Id.Value,
            TenantId = Guid.NewGuid(), SiteId = Guid.NewGuid(), Status = accountStatus
        };
        account.RoleCode = "SITE_OPERATOR";
        user.Accounts.Add(account);
        return user;
    }
}
