using MediatR;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.UserService.Domain.Entities;
using System.Security.Cryptography;
using UserService.API;
using UserService.Application.Authorization;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.DTOs;
using UserService.Application.Usecase.Management;

namespace SmartParking.UserService.Tests;

public sealed class AuthorizationScopeTests
{
    [Theory]
    [InlineData("DRIVER")]
    [InlineData("SITE_OPERATOR")]
    [InlineData("ADMIN")]
    public async Task BoundRoleCannotBorrowOwnerAuthorityFromASiblingAccount(string role)
    {
        var f = await ScopeFixture.CreateAsync(role);
        f.AddAccount("BUSINESS_OWNER", Guid.NewGuid());
        await Forbidden(() => f.Authorization.OwnerScopeAsync(OwnerScopeAction.ParkingManage, default));
        Assert.Equal(0, f.Store.Reads);
    }

    [Fact]
    public async Task BoundAccountRevocationIsNotRescuedByAnotherEligibleMembership()
    {
        var f = await ScopeFixture.CreateAsync("BUSINESS_OWNER");
        f.AddAccount("BUSINESS_OWNER", Guid.NewGuid());
        f.Bound.Status = "LOCKED";
        await Forbidden(() => f.Authorization.OwnerScopeAsync(OwnerScopeAction.ParkingManage, default));
        Assert.Equal(0, f.Store.Reads);
    }

    [Fact]
    public async Task OwnerScopeEvaluatesPermissionsOnEachMembershipAndNeverReturnsAnotherUsersTenant()
    {
        var f = await ScopeFixture.CreateAsync("BUSINESS_OWNER");
        var allowed = f.Bound.TenantId!.Value;
        var denied = f.AddAccount("BUSINESS_OWNER", Guid.NewGuid());
        denied.Permissions = [];
        var backupOnly = f.AddAccount("BUSINESS_OWNER", Guid.NewGuid());
        backupOnly.Permissions = ["BACKUP_MARK"];
        f.AddAccount("DRIVER", Guid.NewGuid());
        f.Store.ExtraAccounts.Add(new Account { Id = Guid.NewGuid(), UserId = Guid.NewGuid(),
            RoleCode = "BUSINESS_OWNER", TenantId = Guid.NewGuid() });

        Assert.Equal(new[] { allowed }, (await f.Authorization.OwnerScopeAsync(OwnerScopeAction.ParkingManage, default)).TenantIds);
        Assert.Equal(new[] { backupOnly.TenantId!.Value }, (await f.Authorization.OwnerScopeAsync(OwnerScopeAction.BackupMark, default)).TenantIds);
        f.Bound.Permissions = [];
        await Forbidden(() => f.Authorization.OwnerScopeAsync(OwnerScopeAction.ParkingManage, default));
    }

    [Fact]
    public async Task OperatorRetainsMultipleAssignedLotsButCannotBorrowAnothersGrant()
    {
        var f = await ScopeFixture.CreateAsync("SITE_OPERATOR");
        f.Grant(f.Bound, "SLOT_OVERRIDE");
        var second = f.AddAccount("SITE_OPERATOR", f.Bound.TenantId, Guid.NewGuid());
        f.Grant(second, "DEVICE_STATUS_VIEW");
        var strangerSite = Guid.NewGuid();
        var stranger = new Account { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), RoleCode = "SITE_OPERATOR",
            TenantId = f.Bound.TenantId, SiteId = strangerSite };
        f.Store.ExtraAccounts.Add(stranger);
        f.Grant(stranger, "SLOT_OVERRIDE");

        Assert.Equal(2, (await f.Authorization.OperatorAssignmentsAsync(f.UserId, default)).Count);
        Assert.Equal(f.Bound.TenantId, Assert.Single((await f.Authorization.OperatorScopeAsync(second.SiteId!.Value,
            OperatorScopeAction.AssignedSite, default)).TenantIds));
        await Forbidden(() => f.Authorization.OperatorScopeAsync(second.SiteId!.Value, OperatorScopeAction.SlotOverride, default));
        await Forbidden(() => f.Authorization.OperatorScopeAsync(strangerSite, OperatorScopeAction.AssignedSite, default));
        await Forbidden(() => f.Authorization.OperatorScopeAsync(Guid.NewGuid(), OperatorScopeAction.AssignedSite, default));

        f.Store.Grants.Remove(f.Bound.Id!.Value);
        await Forbidden(() => f.Authorization.OperatorScopeAsync(f.Bound.SiteId!.Value, OperatorScopeAction.SlotOverride, default));
        f.Parking.InactiveSites.Add(second.SiteId.Value);
        Assert.Empty(await f.Authorization.OperatorAssignmentsAsync(f.UserId, default));
    }

    [Theory]
    [InlineData("OWNER", "BUSINESS_OWNER")]
    [InlineData("OPERATOR", "SITE_OPERATOR")]
    public async Task PublicRoleAliasesDoNotInventLegacyResourceMemberships(string alias, string runtimeRole)
    {
        var f = await ScopeFixture.CreateAsync(alias);
        if (runtimeRole == "BUSINESS_OWNER")
            await Forbidden(() => f.Authorization.OwnerScopeAsync(OwnerScopeAction.ParkingManage, default));
        else
        {
            f.Grant(f.Bound, "SLOT_OVERRIDE");
            Assert.Empty(await f.Authorization.OperatorAssignmentsAsync(f.UserId, default));
        }
    }

    [Fact]
    public async Task AdminKeepsRoleOnlyUserManagementWhileOwnerReviewRetainsItsPermissionGate()
    {
        var f = await ScopeFixture.CreateAsync("ADMIN");
        f.Bound.Permissions = [];
        Assert.Equal("admin", (await f.Authorization.RequireAdminAsync(f.UserId, false, default)).Role);
        await Forbidden(() => f.Authorization.RequireOwnerReviewAsync(f.UserId, false, default));
        f.Bound.Permissions = ["ACCOUNT_ADMIN", "SLOT_OVERRIDE", "PARKING_MANAGE"];
        Assert.Equal("admin", (await f.Authorization.RequireOwnerReviewAsync(f.UserId, false, default)).Role);
        f.AddAccount("BUSINESS_OWNER", Guid.NewGuid());
        await Forbidden(() => f.Authorization.OwnerScopeAsync(OwnerScopeAction.ParkingManage, default));
        await Forbidden(() => f.Authorization.OperatorScopeAsync(Guid.NewGuid(), OperatorScopeAction.SlotOverride, default));
        await Forbidden(() => f.Authorization.DriverAccountsAsync(f.UserId, default));
    }

    [Fact]
    public async Task DriverAuthorityExcludesMixedRoleAccountsAndUsesTheLockingGuard()
    {
        var f = await ScopeFixture.CreateAsync("DRIVER");
        var second = f.AddAccount("DRIVER", null);
        f.AddAccount("BUSINESS_OWNER", Guid.NewGuid());
        var locked = f.AddAccount("DRIVER", null);
        locked.Status = "LOCKED";
        var accounts = await f.Authorization.DriverAccountsAsync(f.UserId, default);
        Assert.Equal(new[] { f.Bound.Id!.Value, second.Id!.Value }.Order(), accounts);
        Assert.True(f.Store.LastLockRows);
        await Forbidden(() => f.Authorization.DriverAccountsAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task CurrentUserRoleGateRunsBeforeTheManagementAdapter()
    {
        var f = await ScopeFixture.CreateAsync("DRIVER");
        var adapter = new UnexpectedUsers();
        var handler = new UserManagementRequestHandler(adapter, f.Current, f.Authorization);
        await Forbidden(() => handler.Handle(new ListUsersQuery(new()), default));
    }

    [Fact]
    public async Task ProductionCompositionResolvesProtectedHandlersWithoutOpeningExternalConnections()
    {
        using var key = RSA.Create(2048);
        await using var app = await UserHost.BuildAsync(["--environment", "Testing"], builder =>
        {
            builder.Host.UseDefaultServiceProvider(options => { options.ValidateOnBuild = true; options.ValidateScopes = true; });
            builder.Logging.ClearProviders();
            builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:User"] = "Host=127.0.0.1;Database=composition_only;Username=composition_only",
                ["Services:Key"] = new string('x', 40),
                ["Jwt:Issuer"] = "composition-tests", ["Jwt:Audience"] = "composition-tests",
                ["Jwt:PrivateKeyPem"] = key.ExportRSAPrivateKeyPem(),
                ["Workflow:KeyDirectory"] = null,
                ["SeedDemoUser"] = "false", ["DevelopmentAdmin:Email"] = null
            });
        });
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        Assert.IsType<AccountAuthorizationService>(services.GetRequiredService<IAccountAuthorizationService>());
        Assert.NotNull(services.GetRequiredService<IRequestHandler<CreateOperatorCommand, OperatorDto>>());
        Assert.NotNull(services.GetRequiredService<IRequestHandler<ReviewOwnerApplicationCommand, OwnerApplicationDto>>());
        Assert.NotNull(services.GetRequiredService<IRequestHandler<SaveOwnVehicleCommand, RegisteredVehicle>>());
        Assert.NotNull(services.GetRequiredService<IRequestHandler<SetupOwnMfaCommand, object>>());
        Assert.NotNull(services.GetRequiredService<IRequestHandler<GetOwnerScopeQuery, IdentityScope>>());
    }

    private static async Task Forbidden(Func<Task> action) =>
        Assert.Equal("FORBIDDEN", (await Assert.ThrowsAsync<AuthException>(action)).Code);

    private sealed class ScopeFixture
    {
        private readonly AuthTestFixture auth = new();
        public Guid UserId => auth.User.Id!.Value;
        public Account Bound => auth.User.Accounts.First();
        public MemoryScopes Store { get; private set; } = null!;
        public TestParkingDirectory Parking { get; } = new();
        public ICurrentAccessService Current { get; private set; } = null!;
        public AccountAuthorizationService Authorization { get; private set; } = null!;
        public static async Task<ScopeFixture> CreateAsync(string role)
        {
            var f = new ScopeFixture();
            f.Bound.RoleCode = role;
            f.Bound.TenantId = Guid.NewGuid();
            if (role is "SITE_OPERATOR" or "OPERATOR") f.Bound.SiteId = Guid.NewGuid();
            await f.auth.SignIn();
            var session = Assert.Single(f.auth.Sessions.Snapshot);
            var actor = new TestCurrentActor(f.UserId, AccessRoles.Map(role), f.Bound.Id, session.AccessTokenId);
            f.Current = TestAccess.Service(f.auth, actor, f.auth.Clock);
            f.Store = new(f.auth.User);
            f.Authorization = new(f.Current, f.Store, f.Parking, NullLogger<AccountAuthorizationService>.Instance);
            return f;
        }
        public Account AddAccount(string role, Guid? tenant, Guid? site = null)
        {
            var account = new Account { Id = Guid.NewGuid(), UserId = UserId, User = auth.User,
                RoleCode = role, TenantId = tenant, SiteId = site };
            auth.User.Accounts.Add(account);
            return account;
        }
        public void Grant(Account account, params string[] permissions)
        {
            Store.Grants[account.Id!.Value] = permissions;
            if (account.SiteId is { } site && account.TenantId is { } tenant) Parking.Sites[site] = tenant;
        }
    }

    private sealed class MemoryScopes(User user) : IAuthorizationScopeStore
    {
        public List<Account> ExtraAccounts { get; } = [];
        public Dictionary<Guid, string[]> Grants { get; } = [];
        public int Reads { get; private set; }
        public bool LastLockRows { get; private set; }
        public Task<IReadOnlyList<AccountMembership>> LoadAsync(Guid userId, bool lockRows, CancellationToken ct)
        {
            Reads++;
            LastLockRows = lockRows;
            return Task.FromResult<IReadOnlyList<AccountMembership>>(user.Accounts.Concat(ExtraAccounts).Select(a =>
                new AccountMembership(a.Id!.Value, a.UserId, a.RoleCode, a.Status, a.DeletedOn,
                    a.TenantId, a.SiteId, a.Permissions, Grants.GetValueOrDefault(a.Id.Value))).ToArray());
        }
    }

    private sealed class UnexpectedUsers : IUserManagementService
    {
        public Task<ManagedUsersPage> ListAsync(Guid adminId, UserSearchDto query, CancellationToken ct) => throw new InvalidOperationException("Unauthorized adapter call.");
        public Task<ManagedUserDetail> GetAsync(Guid adminId, Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<ManagedUserDto> SetStatusAsync(Guid adminId, Guid id, UserStatusDto body, CancellationToken ct) => throw new NotSupportedException();
    }
}
