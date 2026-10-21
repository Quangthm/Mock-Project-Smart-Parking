using UserService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Models.JwT;
using UserService.Application.Services;
using UserService.Application.Usecase.Login;
using UserService.Persistence.Repositories;

namespace SmartParking.UserService.Tests;

public sealed class IdentityRegressionTests
{
    [PostgresFact]
    public async Task RefreshWinningAfterLogoutAuthenticationCannotKeepLogicalSessionAlive()
    {
        await using var fixture=await UserServiceDatabaseFixture.CreateAsync();
        var clock=new TestClock();var user=User("logout-race@example.com","DRIVER",clock);
        var initial=new AuthSession{Id=Guid.NewGuid(),UserId=user.Id!.Value,AccessTokenId=Guid.NewGuid(),TokenHash="first-hash",
            CreatedAt=clock.Now,LastRefreshedAt=clock.Now,AccessExpiresAt=clock.Now.AddMinutes(15),ExpiresAt=clock.Now.AddDays(7)};
        await using(var db=fixture.Open()){db.Users.Add(user);db.AuthSessions.Add(initial);await db.SaveChangesAsync();}
        var rotatedId=Guid.NewGuid();
        await using(var db=fixture.Open())
        {
            var store=new PostgresAuthSessionStore(db,clock);
            Assert.True(await store.IsActiveAsync(initial.AccessTokenId,user.Id.Value,default)); // logout auth has passed
            Assert.True(await store.RotateAsync(initial,rotatedId,"second-hash",clock.Now.AddMinutes(15),default));
        }
        await using(var db=fixture.Open())
        {
            var store=new PostgresAuthSessionStore(db,clock);
            Assert.Null(await store.FindByRefreshHashAsync(initial.TokenHash,default));
            Assert.False(await store.IsActiveAsync(initial.AccessTokenId,user.Id.Value,default));
            Assert.False(await store.RevokeAsync(initial.AccessTokenId,user.Id.Value,"wrong-hash",default));
            Assert.True(await store.RevokeAsync(initial.AccessTokenId,user.Id.Value,initial.TokenHash,default));
            Assert.False(await store.IsActiveAsync(rotatedId,user.Id.Value,default));
            var rotated=(await store.FindByRefreshHashAsync("second-hash",default))!;
            Assert.False(await store.RotateAsync(rotated,Guid.NewGuid(),"third-hash",clock.Now.AddMinutes(15),default));
        }
    }

    [PostgresFact]
    public async Task LegacyOperatorTimedOrAdminUnlockCannotBypassContactVerification()
    {
        await using var fixture=await UserServiceDatabaseFixture.CreateAsync();
        var clock=new TestClock();var passwords=new BcryptPasswordService();
        var admin=User("admin-unlock@example.com","ADMIN",clock);
        var timed=User("operator-timed@example.com","SITE_OPERATOR",clock);
        var blocked=User("operator-blocked@example.com","SITE_OPERATOR",clock);
        foreach(var user in new[]{timed,blocked}){user.EmailVerifiedAt=null;user.Status=UserStatus.Locked;}
        timed.LockedUntil=clock.Now.AddMinutes(-1);
        await using(var db=fixture.Open()){db.Users.AddRange(admin,timed,blocked);await db.SaveChangesAsync();}
        await using(var db=fixture.Open())
        {
            var policy=new AuthenticationPolicy();var options=new JwtOptions{Issuer="unlock",Audience="unlock"};
            using var keys=new JwtKeyProvider(options,true);var store=new PostgresAuthSessionStore(db,clock);
            var sessions=new AuthSessionService(new AccessTokenService(options,keys,store,clock),store,policy,clock);
            var handler=new LoginCommandHandler(new UnitOfWork(db,new UserRepository(db)),passwords,sessions,policy,clock);
            var result=await handler.Handle(new(){Email=timed.Email!,Password="Password@123"},default);
            Assert.Equal("ACCOUNT_PENDING_VERIFICATION",result.ErrorCode);
            Assert.Empty(await db.AuthSessions.ToArrayAsync());
        }
        await using(var db=fixture.Open())
        {
            await new UserManagementService(db,clock).SetStatusAsync(admin.Id!.Value,blocked.Id!.Value,new(){Status="active"},default);
            var user=await db.Users.Include(u=>u.Accounts).ThenInclude(a=>a.AccountRoles).SingleAsync(u=>u.Id==blocked.Id);
            Assert.Equal(UserStatus.PendingVerification,user.Status);
            Assert.Null(AccountRolePolicy.CurrentRole(user));
            Assert.All(user.Accounts,a=>Assert.Equal("PENDING_VERIFICATION",a.Status));
        }
    }

    [PostgresFact]
    public async Task RoleDefaultsNeverOverrideExplicitEmptyOrBackupMarkOptIn()
    {
        await using var fixture=await UserServiceDatabaseFixture.CreateAsync();var clock=new TestClock();
        var owner=User("defaults@example.com","BUSINESS_OWNER",clock);var tenant=Guid.NewGuid();
        owner.Accounts.Single().TenantId=tenant;
        await fixture.SeedTenantsAsync(tenant);
        await using var db=fixture.Open();db.Users.Add(owner);await db.SaveChangesAsync();
        var repository=new IdentityAccessRepository(db);
        Assert.Equal(new[]{tenant},await repository.OwnerTenantsAsync(owner.Id!.Value,"OPERATOR_MANAGE",default));
        Assert.Empty(await repository.OwnerTenantsAsync(owner.Id.Value,"BACKUP_MARK",default));
        await db.Accounts.Where(a=>a.UserId==owner.Id).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.Permissions,new[]{"BACKUP_MARK"}));
        Assert.Equal(new[]{tenant},await repository.OwnerTenantsAsync(owner.Id.Value,"BACKUP_MARK",default));
        Assert.Empty(await repository.OwnerTenantsAsync(owner.Id.Value,"OPERATOR_MANAGE",default));
        await db.Accounts.Where(a=>a.UserId==owner.Id).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.Permissions,Array.Empty<string>()));
        Assert.Empty(await repository.OwnerTenantsAsync(owner.Id.Value,"BACKUP_MARK",default));
    }
    private static User User(string email,string role,TestClock clock)
    {
        var user=new User{Id=Guid.NewGuid(),Email=email,FullName="Test User",EmailVerifiedAt=clock.Now,Status=UserStatus.Active,
            PasswordHash=new BcryptPasswordService().Hash("Password@123"),CreatedOn=clock.Now,ModifiedOn=clock.Now};
        var account=new Account{Id=Guid.NewGuid(),User=user,UserId=user.Id.Value,Status="ACTIVE"};
        account.AccountRoles.Add(new(){Account=account,AccountId=account.Id.Value,RoleCode=role});user.Accounts.Add(account);return user;
    }
}
