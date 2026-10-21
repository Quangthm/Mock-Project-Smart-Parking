using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Services;

namespace SmartParking.UserService.Tests;

public sealed class IdentityMigrationTests
{
    [PostgresFact]
    public async Task HardeningPreservesVerifiedPhoneAndDemotesOnlyUnverifiedLegacyOperators()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        var verifiedAt = new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
        var phoneVerified = Operator("phone-verified@example.com", "+84912345678", verifiedAt);
        var unverified = Operator("unverified@example.com", "+84912345679", null);
        await fixture.SeedTenantsAsync(phoneVerified.Accounts.Single().TenantId!.Value, unverified.Accounts.Single().TenantId!.Value);
        await using (var db = fixture.Open())
        {
            db.Users.AddRange(phoneVerified, unverified);
            await db.SaveChangesAsync();
        }

        // Run the real deployment migration against existing legacy rows, not a copied predicate.
        await using (var connection = new NpgsqlConnection(fixture.ConnectionString))
        {
            await connection.OpenAsync();
            var sql = await File.ReadAllTextAsync(ServiceSchema.FeaturePath("05.14-Identity-Access-Hardening.sql"));
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }

        await using var verification = fixture.Open();
        var users = await verification.Users.AsNoTracking().Include(user => user.Accounts)
            .ThenInclude(account => account.AccountRoles).ToDictionaryAsync(user => user.Id!.Value);
        var retained = users[phoneVerified.Id!.Value];
        Assert.Equal(UserStatus.Active, retained.Status);
        Assert.Equal(verifiedAt, retained.PhoneVerifiedAt);
        Assert.Null(retained.EmailVerifiedAt);
        Assert.All(retained.Accounts, account => Assert.Equal("ACTIVE", account.Status));
        Assert.Equal("operator", AccountRolePolicy.CurrentRole(retained));

        var pending = users[unverified.Id!.Value];
        Assert.Equal(UserStatus.PendingVerification, pending.Status);
        Assert.Null(pending.EmailVerifiedAt);
        Assert.Null(pending.PhoneVerifiedAt);
        Assert.All(pending.Accounts, account => Assert.Equal("PENDING_VERIFICATION", account.Status));
        Assert.Null(AccountRolePolicy.CurrentRole(pending));
    }

    private static User Operator(string email, string phone, DateTimeOffset? phoneVerifiedAt)
    {
        var user = new User
        {
            Id = Guid.NewGuid(), Email = email, Phone = phone, FullName = "Legacy Operator",
            PasswordHash = "unused-migration-test-hash", Status = UserStatus.Active,
            PhoneVerifiedAt = phoneVerifiedAt
        };
        var account = new Account
        {
            Id = Guid.NewGuid(), User = user, UserId = user.Id.Value,
            TenantId = Guid.NewGuid(), SiteId = Guid.NewGuid(), Status = "ACTIVE"
        };
        account.AccountRoles.Add(new AccountRole
        {
            Account = account, AccountId = account.Id.Value, RoleCode = "SITE_OPERATOR"
        });
        user.Accounts.Add(account);
        return user;
    }
}
