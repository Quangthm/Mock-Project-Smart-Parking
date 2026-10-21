using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.DTOs;
using UserService.Application.Services;
using UserService.Infrastructure.Services;
using UserService.Persistence;

namespace SmartParking.UserService.Tests;

public sealed class OwnerTenantApprovalTests
{
    [PostgresFact]
    public async Task FailedFinalizeRollsBackTenantAndRetryActivatesExactlyOnce()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        var (admin, application) = await SeedAsync(fixture);
        var parking = new TestParkingDirectory();
        // Fail after account/application UPDATEs have reached PostgreSQL, while queuing
        // the decision. The raw tenant INSERT must roll back with those writes.
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(fixture.ConnectionString)
            .AddInterceptors(new FailDecisionSave()).Options;
        await using (var failing = new AppDbContext(options))
            await Assert.ThrowsAsync<IOException>(() => Service(failing, parking).ReviewAsync(admin, application.Id, new() { Status = "approved" }, default));
        Assert.Contains(application.Id, parking.Provisioned);
        await using (var check = fixture.Open())
        {
            Assert.Equal(0, await TenantCountAsync(check, application.Id));
            Assert.Equal("pending", (await check.OwnerApplications.SingleAsync()).Status);
            Assert.NotNull((await check.OwnerApplications.SingleAsync()).ApprovalRequestedBy);
            var owner = await check.Users.Include(u => u.Accounts).SingleAsync(u => u.Id == application.UserId);
            Assert.Equal(UserStatus.PendingApproval, owner.Status);
            Assert.False(owner.OnboardingComplete);
            Assert.Null(owner.Accounts.Single().TenantId);
            Assert.Equal("PENDING_APPROVAL", owner.Accounts.Single().Status);
            Assert.Empty(await check.WorkflowDeliveries.ToListAsync());
        }
        async Task<OwnerApplicationDto> Approve()
        {
            await using var db = fixture.Open();
            return await Service(db, parking).ReviewAsync(admin, application.Id, new() { Status = "approved" }, default);
        }
        var results = await Task.WhenAll(Approve(), Approve());
        Assert.All(results, r => Assert.Equal("approved", r.Status));
        Assert.Equal(results[0].DeliveryId, results[1].DeliveryId);
        await Approve();
        await using var final = fixture.Open();
        Assert.Equal(1, await TenantCountAsync(final, application.Id));
        var active = await final.Users.Include(u => u.Accounts).SingleAsync(u => u.Id == application.UserId);
        Assert.Equal(UserStatus.Active, active.Status);
        Assert.True(active.OnboardingComplete);
        Assert.Equal(application.Id, active.Accounts.Single().TenantId);
        Assert.Equal("ACTIVE", active.Accounts.Single().Status);
        Assert.Single(await final.WorkflowDeliveries.Where(d => d.Kind == "OWNER_DECISION").ToListAsync());
    }

    [PostgresFact]
    public async Task ExistingTenantIsReusedOnlyWhenMatchingAndActive()
    {
        foreach (var scenario in new[] { "matching", "name", "email", "phone", "inactive", "suspended", "deleted", "code_collision" })
        {
            await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
            var (admin, application) = await SeedAsync(fixture);
            var tenantId = scenario == "code_collision" ? Guid.NewGuid() : application.Id;
            var name = scenario == "name" ? "Another business" : application.BusinessName;
            var email = scenario == "email" ? "another@example.com" : application.User.Email!;
            var phone = scenario == "phone" ? "0999999999" : application.User.Phone!;
            var status = scenario == "inactive" ? "INACTIVE" : scenario == "suspended" ? "SUSPENDED" : "ACTIVE";
            var code = "T-" + application.Id.ToString("N");
            DateTimeOffset? deleted = scenario == "deleted" ? DateTimeOffset.UtcNow : null;
            await using (var db = fixture.Open())
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO tenants(id,code,name,contact_email,contact_phone,status,deleted_at)
                    VALUES ({tenantId},{code},{name},{email},{phone},{status},{deleted})
                    """);
            await using (var db = fixture.Open())
            {
                var review = Service(db, new TestParkingDirectory()).ReviewAsync(admin, application.Id, new() { Status = "approved" }, default);
                if (scenario == "matching") Assert.Equal("approved", (await review).Status);
                else Assert.Equal("APPROVAL_CONFLICT", (await Assert.ThrowsAsync<AuthException>(() => review)).Code);
            }
            await using var check = fixture.Open();
            // No overwrite/reactivation of the pre-existing record, even on conflict.
            Assert.True(await check.Database.SqlQuery<bool>($"""
                SELECT (name={name} AND contact_email={email} AND contact_phone={phone}
                    AND status={status} AND (deleted_at IS NOT NULL)={deleted.HasValue}) AS "Value"
                FROM tenants WHERE id={tenantId}
                """).SingleAsync());
            Assert.Equal(1, await check.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM tenants").SingleAsync());
            Assert.Equal(scenario == "matching" ? "approved" : "pending", (await check.OwnerApplications.SingleAsync()).Status);
            Assert.Equal(scenario == "matching" ? 1 : 0, await check.WorkflowDeliveries.CountAsync());
        }
    }

    [PostgresFact]
    public async Task RejectionDoesNotCreateTenantOrProvisionSite()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        var (admin, application) = await SeedAsync(fixture);
        var parking = new TestParkingDirectory();
        await using (var db = fixture.Open())
            Assert.Equal("rejected", (await Service(db, parking).ReviewAsync(admin, application.Id, new() { Status = "rejected" }, default)).Status);
        await using var check = fixture.Open();
        Assert.Equal(0, await TenantCountAsync(check, application.Id));
        Assert.Empty(parking.Provisioned);
    }

    private static Task<int> TenantCountAsync(AppDbContext db, Guid id) =>
        db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM tenants WHERE id={id}").SingleAsync();

    private static OwnerRegistrationService Service(AppDbContext db, TestParkingDirectory parking) =>
        new(db, new BcryptPasswordService(), TimeProvider.System, parking, WorkflowTestSupport.Create(db, TimeProvider.System));

    private static async Task<(Guid Admin, OwnerApplication Application)> SeedAsync(UserServiceDatabaseFixture fixture)
    {
        await using var db = fixture.Open();
        var admin = new User { Id = Guid.NewGuid(), FullName = "Admin", Email = "admin@example.com", PasswordHash = "unused", Status = UserStatus.Active };
        admin.Accounts.Add(new Account { Id = Guid.NewGuid(), User = admin, UserId = admin.Id.Value,
            AccountRoles = [new AccountRole { RoleCode = "PLATFORM_ADMIN" }] });
        var owner = new User { Id = Guid.NewGuid(), FullName = "Owner", Email = "owner@example.com", Phone = "0912345678",
            PasswordHash = "unused", Status = UserStatus.PendingApproval, EmailVerifiedAt = DateTimeOffset.UtcNow };
        owner.Accounts.Add(new Account { Id = Guid.NewGuid(), User = owner, UserId = owner.Id.Value, Status = "PENDING_APPROVAL",
            AccountRoles = [new AccountRole { RoleCode = "BUSINESS_OWNER" }] });
        var application = new OwnerApplication { Id = Guid.NewGuid(), UserId = owner.Id.Value, User = owner,
            BusinessName = "Owner business", LotType = "outdoor", SubmittedAt = DateTimeOffset.UtcNow,
            LotSetupJson = JsonSerializer.Serialize(new OwnerLotSetupDto { Name = "Lot", Address = "Address", LotType = "outdoor" }) };
        db.Users.Add(admin);
        db.OwnerApplications.Add(application);
        await db.SaveChangesAsync();
        return (admin.Id.Value, application);
    }

    private sealed class FailDecisionSave : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<WorkflowDelivery>().Any(e => e.State == EntityState.Added && e.Entity.Kind == "OWNER_DECISION"))
                throw new IOException("Simulated failure while saving decision delivery.");
            return ValueTask.FromResult(result);
        }
    }
}
