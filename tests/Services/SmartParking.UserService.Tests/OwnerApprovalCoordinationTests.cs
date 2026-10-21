using System.Reflection;
using System.Text.Json;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.DTOs;
using UserService.Application.Services;
using UserService.Infrastructure.Services;

namespace SmartParking.UserService.Tests;

public sealed class OwnerApprovalCoordinationTests
{
    [Fact]
    public async Task RemoteProvisioningOccursOutsideTransactionAndCompletedReplayDoesNotRepeatIt()
    {
        var f = new Fixture();
        var first = await f.Approve();
        var replay = await f.Approve();
        Assert.Equal("approved", first.Status);
        Assert.Equal(first.Id, replay.Id);
        Assert.Equal(first.DeliveryId, replay.DeliveryId);
        Assert.Equal(1, f.RemoteCalls);
        Assert.Single(f.Store.Deliveries);
        Assert.False(f.Store.InTransaction);
        Assert.True(f.Store.TenantExists);
    }

    [Fact]
    public async Task LostRemoteResponseKeepsIntentAndRetryUsesSameOperation()
    {
        var f = new Fixture { LoseResponse = true };
        await Assert.ThrowsAsync<HttpRequestException>(() => f.Approve());
        Assert.Equal("pending", f.Store.Application.Status);
        Assert.NotNull(f.Store.Application.ApprovalRequestedBy);
        Assert.Empty(f.Store.Deliveries);
        Assert.False(f.Store.InTransaction);
        f.LoseResponse = false;
        Assert.Equal("approved", (await f.Approve()).Status);
        Assert.Equal(2, f.RemoteCalls);
        Assert.Single(f.OperationIds.Distinct());
        Assert.Single(f.Store.Deliveries);
    }

    [Fact]
    public async Task FailedLocalFinalizeCanRetryWithoutLosingDurableIntent()
    {
        var f = new Fixture();
        f.Store.FailFinalizeCommit = true;
        await Assert.ThrowsAsync<IOException>(() => f.Approve());
        Assert.Equal("pending", f.Store.Application.Status);
        Assert.NotNull(f.Store.Application.ApprovalRequestedBy);
        Assert.Empty(f.Store.Deliveries);
        Assert.False(f.Store.TenantExists);
        f.Store.FailFinalizeCommit = false;
        Assert.Equal("approved", (await f.Approve()).Status);
        Assert.True(f.Store.TenantExists);
        Assert.Single(f.OperationIds.Distinct());
        Assert.Single(f.Store.Deliveries);
    }

    [Fact]
    public async Task RevokedAdminCannotReadCompletedReplay()
    {
        var f = new Fixture();
        await f.Approve();
        f.Store.AdminAllowed = false;
        var error = await Assert.ThrowsAsync<AuthException>(() => f.Approve());
        Assert.Equal("FORBIDDEN", error.Code);
        Assert.Equal(1, f.RemoteCalls);
    }

    private sealed class Fixture : IParkingDirectory
    {
        public readonly StoreProxy Store;
        private readonly OwnerRegistrationService service;
        private readonly Guid admin = Guid.NewGuid();
        public bool LoseResponse;
        public int RemoteCalls;
        public readonly List<Guid> OperationIds = [];
        public Fixture()
        {
            var port = DispatchProxy.Create<IIdentityWorkflowStore, StoreProxy>();
            Store = (StoreProxy)port;
            var passwords = new BcryptPasswordService();
            var workflows = new AccountWorkflowService(port, passwords, new TestWorkflowProtector(), TimeProvider.System);
            service = new(port, passwords, TimeProvider.System, this, workflows);
        }
        public Task<OwnerApplicationDto> Approve() => service.ReviewAsync(admin, Store.Application.Id, new() { Status = "approved" }, default);
        public Task ProvisionOwnerSiteAsync(Guid applicationId, Guid tenantId, string name, string email, string phone, OwnerLotSetupDto lot, CancellationToken ct)
        {
            Assert.False(Store.InTransaction);
            Assert.NotNull(Store.Application.ApprovalRequestedBy);
            Assert.Equal(Store.Application.Id, applicationId);
            Assert.Equal(applicationId, tenantId);
            RemoteCalls++;
            OperationIds.Add(applicationId);
            return LoseResponse ? Task.FromException(new HttpRequestException("response lost")) : Task.CompletedTask;
        }
        public Task ProvisionTenantAsync(Guid id, string name, string email, string phone, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<DirectorySite>> ActiveSitesAsync(Guid[] sites, Guid[] tenants, CancellationToken ct) => throw new NotSupportedException();
    }

    // Strict application-port fake: unexpected queries fail, and uncommitted writes
    // roll back. This tests coordination without pretending to test PostgreSQL locks.
    public class StoreProxy : DispatchProxy
    {
        public OwnerApplication Application = new()
        {
            Id = Guid.NewGuid(), UserId = Guid.NewGuid(), BusinessName = "Owner business",
            LotSetupJson = JsonSerializer.Serialize(new OwnerLotSetupDto { Name = "Lot", Address = "Address" })
        };
        public User User;
        public readonly List<WorkflowDelivery> Deliveries = [];
        public bool InTransaction, FailFinalizeCommit, TenantExists, AdminAllowed = true;
        public StoreProxy()
        {
            User = new() { Id = Application.UserId, FullName = "Owner", Email = "owner@example.com", Phone = "0901234567", EmailVerifiedAt = DateTimeOffset.UtcNow, Status = UserStatus.PendingApproval };
            var account = new Account { Id = Guid.NewGuid(), UserId = User.Id.Value, User = User, Status = "PENDING_APPROVAL" };
            account.AccountRoles.Add(new AccountRole { Account = account, AccountId = account.Id!.Value, RoleCode = "BUSINESS_OWNER" });
            User.Accounts.Add(account);
        }
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
        {
            "BeginTransactionAsync" => Begin(),
            "LockPermissionRowsAsync" => Task.FromResult(AdminAllowed ? new List<Guid> { Guid.NewGuid() } : []),
            "IsActiveAdminAsync" => Task.FromResult(AdminAllowed),
            "LockOwnerApplicationAsync" => Task.FromResult<OwnerApplication?>(Application),
            "LockUserAsync" => Task.FromResult(User),
            "LockOwnerAccountsAsync" => Task.CompletedTask,
            "HasPendingOwnerAccountAsync" => Task.FromResult(true),
            "EnsureOwnerTenantAsync" => EnsureTenant(args!),
            "SaveChangesAsync" => Task.FromResult(1),
            "ReadOwnerDecisionDeliveriesAsync" => Task.FromResult(Deliveries.ToArray()),
            "ResetTracking" => null,
            "Stage" => Stage(args![0]!),
            _ => throw new NotSupportedException(method.Name)
        };
        private object? Stage(object value) { Deliveries.Add(Assert.IsType<WorkflowDelivery>(value)); return null; }
        private Task EnsureTenant(object?[] args)
        {
            Assert.True(InTransaction);
            Assert.Equal(Application.Id, (Guid)args[0]!);
            Assert.Equal("pending", Application.Status);
            TenantExists = true;
            return Task.CompletedTask;
        }
        private Task<IUnitOfWorkTransaction> Begin()
        {
            Assert.False(InTransaction);
            InTransaction = true;
            return Task.FromResult<IUnitOfWorkTransaction>(new Transaction(this));
        }
        private sealed class Transaction(StoreProxy owner) : IUnitOfWorkTransaction
        {
            private readonly string status = owner.Application.Status;
            private readonly Guid? intent = owner.Application.ApprovalRequestedBy;
            private readonly UserStatus userStatus = owner.User.Status;
            private readonly string accountStatus = owner.User.Accounts.Single().Status;
            private readonly int deliveryCount = owner.Deliveries.Count;
            private readonly bool tenantExists = owner.TenantExists;
            private bool committed;
            public Task CommitAsync(CancellationToken ct)
            {
                if (owner.FailFinalizeCommit && owner.Application.Status == "approved") throw new IOException("commit failed");
                committed = true;
                return Task.CompletedTask;
            }
            public ValueTask DisposeAsync()
            {
                if (!committed)
                {
                    owner.Application.Status = status;
                    owner.Application.ApprovalRequestedBy = intent;
                    owner.User.Status = userStatus;
                    owner.User.Accounts.Single().Status = accountStatus;
                    owner.TenantExists = tenantExists;
                    owner.Deliveries.RemoveRange(deliveryCount, owner.Deliveries.Count - deliveryCount);
                }
                owner.InTransaction = false;
                return ValueTask.CompletedTask;
            }
        }
    }
}
