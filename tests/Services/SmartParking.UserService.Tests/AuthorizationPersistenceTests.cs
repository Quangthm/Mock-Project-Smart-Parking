using Microsoft.EntityFrameworkCore;
using SmartParking.UserService.Domain.Entities;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.DTOs;
using UserService.Infrastructure.Services;
using UserService.Persistence.Repositories;

namespace SmartParking.UserService.Tests;

public sealed class AuthorizationPersistenceTests
{
    [PostgresFact]
    public async Task ProvisioningAssignsOneRolePerSiteAndScopeReadsRespectCurrentMembershipsAndGrants()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        await using var db = fixture.Open();
        var clock = new TestClock();
        var parking = new TestParkingDirectory();
        var allowedTenant = Guid.NewGuid();
        var deniedTenant = Guid.NewGuid();
        foreach (var tenant in new[] { allowedTenant, deniedTenant })
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO tenants(id,code,name,contact_email,contact_phone)
                VALUES({tenant},{tenant.ToString("N")},'Scope fixture','scope@example.test','0900000000')
                """);
        var owner = new User { Id = Guid.NewGuid(), FullName = "Owner", PasswordHash = "unused",
            Accounts = [
                new Account { Id = Guid.NewGuid(), RoleCode = "BUSINESS_OWNER", TenantId = allowedTenant,
                    Permissions = ["OPERATOR_MANAGE", "PARKING_MANAGE"] },
                new Account { Id = Guid.NewGuid(), RoleCode = "BUSINESS_OWNER", TenantId = deniedTenant, Permissions = [] }
            ] };
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        var allowedAccount = owner.Accounts.Single(a => a.TenantId == allowedTenant);
        var ownerAccess = await TestAuthorization.ForUserAsync(db, clock, owner.Id!.Value, parking, allowedAccount.Id);
        var workflows = WorkflowTestSupport.Create(db, clock, ownerAccess);
        var provisioning = new OperatorProvisioningService(db, new BcryptPasswordService(), clock, workflows, ownerAccess);
        var siteA = Guid.NewGuid();
        var siteB = Guid.NewGuid();
        var deniedSite = Guid.NewGuid();
        parking.Sites[siteA] = allowedTenant;
        parking.Sites[siteB] = allowedTenant;
        parking.Sites[deniedSite] = deniedTenant;
        CreateOperatorDto Request(params Guid[] sites) => new() { FullName = "Operator", Email = "operator@example.test",
            Password = "Password@123", SiteIds = sites, Permissions = ["DEVICE_MANAGE"] };

        // Permission from the first membership cannot authorize the second tenant.
        Assert.Equal("SITE_ACCESS_DENIED", (await Assert.ThrowsAsync<AuthException>(() =>
            provisioning.CreateAsync(owner.Id.Value, Request(deniedSite), default))).Code);
        Assert.Empty(await db.OperatorGrants.ToListAsync());
        var created = await provisioning.CreateAsync(owner.Id.Value, Request(siteA, siteB), default);
        var accounts = await db.Accounts.AsNoTracking().Include(a => a.Role)
            .Where(a => a.UserId == created.Id).OrderBy(a => a.Id).ToArrayAsync();
        Assert.Equal(2, accounts.Length);
        Assert.All(accounts, a => { Assert.Equal("SITE_OPERATOR", a.RoleCode); Assert.Equal(a.RoleCode, a.Role.Code); });
        Assert.Equal(new[] { siteA, siteB }.Order(), accounts.Select(a => a.SiteId!.Value).Order());
        Assert.Equal(2, await db.OperatorGrants.CountAsync());

        var bound = accounts[0];
        var target = accounts[1];
        var operatorAccess = await TestAuthorization.ForUserAsync(db, clock, created.Id, parking, bound.Id);
        Assert.Equal(2, (await operatorAccess.OperatorAssignmentsAsync(created.Id, default)).Count);
        await db.OperatorGrants.Where(g => g.AccountId == target.Id).ExecuteDeleteAsync();
        Assert.Equal("FORBIDDEN", (await Assert.ThrowsAsync<AuthException>(() =>
            operatorAccess.OperatorScopeAsync(target.SiteId!.Value, OperatorScopeAction.AssignedSite, default))).Code);
        Assert.Equal(allowedTenant, Assert.Single((await operatorAccess.OperatorScopeAsync(bound.SiteId!.Value,
            OperatorScopeAction.AssignedSite, default)).TenantIds));
        await db.Accounts.Where(a => a.Id == bound.Id).ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, "LOCKED"));
        Assert.Equal("FORBIDDEN", (await Assert.ThrowsAsync<AuthException>(() =>
            operatorAccess.OperatorAssignmentsAsync(created.Id, default))).Code);
    }
}
