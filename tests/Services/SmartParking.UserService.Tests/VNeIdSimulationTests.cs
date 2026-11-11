using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.DTOs;
using UserService.Application.Services;
using Xunit;

namespace SmartParking.UserService.Tests;

public sealed class VNeIdSimulationTests
{
    private sealed class MemoryTransaction : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken ct) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeWorkflowStore : IIdentityWorkflowStore
    {
        public readonly Dictionary<Guid, User> Users = new();
        public readonly Dictionary<Guid, DriverRegistration> DriverRegs = new();
        public readonly Dictionary<Guid, OwnerApplication> OwnerApps = new();
        public readonly Dictionary<Guid, UserSimulatedIdentity> SimulatedIdentities = new();

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct) => Task.FromResult<IUnitOfWorkTransaction>(new MemoryTransaction());
        public Task CommitCurrentTransactionAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<int> SaveChangesAsync(CancellationToken ct) => Task.FromResult(1);
        public Task SaveRegistrationAsync(CancellationToken ct) => Task.CompletedTask;
        public void Stage<T>(T entity) where T : class
        {
            if (entity is UserSimulatedIdentity sim) SimulatedIdentities[sim.UserId] = sim;
            if (entity is User u) Users[u.Id!.Value] = u;
            if (entity is DriverRegistration dr) DriverRegs[dr.Id] = dr;
            if (entity is OwnerApplication oa) OwnerApps[oa.Id] = oa;
        }

        public Task<User> LockUserAsync(Guid id, CancellationToken ct) => Task.FromResult(Users[id]);
        public Task<DriverRegistration?> LockDriverRegistrationAsync(Guid id, CancellationToken ct) => Task.FromResult(DriverRegs.GetValueOrDefault(id));
        public Task<OwnerApplication?> LockOwnerApplicationAsync(Guid id, CancellationToken ct) => Task.FromResult(OwnerApps.GetValueOrDefault(id));
        public Task<UserSimulatedIdentity?> LockSimulatedIdentityAsync(Guid userId, CancellationToken ct) => Task.FromResult(SimulatedIdentities.GetValueOrDefault(userId));
        public Task<UserSimulatedIdentity?> FindSimulatedIdentityAsync(Guid userId, CancellationToken ct) => Task.FromResult(SimulatedIdentities.GetValueOrDefault(userId));

        // Stubs for interface
        public Task<int> CancelChallengeDeliveriesAsync(Guid previous, CancellationToken ct) => Task.FromResult(0);
        public Task<User?> LockPendingOwnerByContactAsync(string contact, CancellationToken ct) => Task.FromResult<User?>(null);
        public Task<OwnerApplication?> FindOwnerApplicationByUserAsync(Guid? userId, CancellationToken ct) => Task.FromResult<OwnerApplication?>(null);
        public Task<AuthChallenge?> FindOwnerContactAsync(Guid? userId, CancellationToken ct) => Task.FromResult<AuthChallenge?>(null);
        public Task<OwnerApplication?> FindOwnerApplicationByEmailAsync(string email, CancellationToken ct) => Task.FromResult<OwnerApplication?>(null);
        public Task<AuthChallenge?> FindOwnerSetupRecoveryChallengeAsync(Guid userId, CancellationToken ct) => Task.FromResult<AuthChallenge?>(null);
        public Task<bool> HasOwnerSetupAsync(Guid userId, CancellationToken ct) => Task.FromResult(false);
        public Task<User?> LockLoginUserByContactAsync(string contact, CancellationToken ct) => Task.FromResult<User?>(null);
        public Task<AuthChallenge?> FindOutstandingLoginAsync(Guid? userId, CancellationToken ct) => Task.FromResult<AuthChallenge?>(null);
        public Task<Guid?> FindChallengeUserIdAsync(Guid id, CancellationToken ct) => Task.FromResult<Guid?>(null);
        public Task<AuthChallenge?> LockChallengeAsync(Guid id, CancellationToken ct) => Task.FromResult<AuthChallenge?>(null);
        public Task<int> CloseOwnerPhoneChallengesAsync(Guid userId, DateTimeOffset now, CancellationToken ct) => Task.FromResult(0);
        public Task<int> CancelOwnerPhoneDeliveriesAsync(Guid userId, CancellationToken ct) => Task.FromResult(0);
        public Task<User> LockUserWithRolesAsync(Guid id, CancellationToken ct) => Task.FromResult(Users[id]);
        public Task<MfaCredential?> LockMfaCredentialAsync(Guid id, CancellationToken ct) => Task.FromResult<MfaCredential?>(null);
        public Task<WorkflowDelivery?> LockDeliveryAsync(Guid id, CancellationToken ct) => Task.FromResult<WorkflowDelivery?>(null);
        public Task<Guid?[]> ReadInvitationTenantsAsync(Guid userId, CancellationToken ct) => Task.FromResult(Array.Empty<Guid?>());
        public Task<List<WorkflowDelivery>> LockDueDeliveryAsync(DateTimeOffset now, CancellationToken ct) => Task.FromResult(new List<WorkflowDelivery>());
        public ValueTask<AuthChallenge?> FindChallengeAsync(Guid challengeId, CancellationToken ct) => ValueTask.FromResult<AuthChallenge?>(null);
        public Task<bool> IsMfaEnabledAsync(Guid id, CancellationToken ct) => Task.FromResult(false);
        public Task<int> CountUnusedRecoveryCodesAsync(Guid id, CancellationToken ct) => Task.FromResult(0);
        public Task<int> DeleteRecoveryCodesAsync(Guid id, CancellationToken ct) => Task.FromResult(0);
        public Task<MfaRecoveryCode?> FindUnusedRecoveryCodeAsync(Guid userId, string hash, CancellationToken ct) => Task.FromResult<MfaRecoveryCode?>(null);
        public Task<int> CloseLoginChallengesAsync(Guid id, DateTimeOffset now, CancellationToken ct) => Task.FromResult(0);
        public Task<int> RevokeMfaSessionsAsync(Guid id, CancellationToken ct) => Task.FromResult(0);
        public Task<bool> ContactExistsAsync(string? email, string? phone, CancellationToken ct) => Task.FromResult(false);
        public Task<User?> FindPendingDriverAsync(string contact, CancellationToken ct) => Task.FromResult<User?>(null);
        public Task<DriverRegistration?> FindPendingDriverRegistrationAsync(Guid? userId, CancellationToken ct) => Task.FromResult<DriverRegistration?>(null);
        public Task<bool> OwnerContactExistsAsync(string email, string phone, CancellationToken ct) => Task.FromResult(false);
        public Task<AuthChallenge> GetOwnerContactAsync(Guid userId, CancellationToken ct) => Task.FromResult(new AuthChallenge());
        public Task<bool> IsActiveAdminAsync(Guid id, CancellationToken ct) => Task.FromResult(false);
        public Task<List<OwnerApplication>> ListOwnerApplicationsAsync(CancellationToken ct) => Task.FromResult(new List<OwnerApplication>());
        public Task<Guid[]> ReadVerifiedUserIdsAsync(CancellationToken ct) => Task.FromResult(Array.Empty<Guid>());
        public Task<WorkflowDelivery[]> ReadOwnerDecisionDeliveriesAsync(CancellationToken ct) => Task.FromResult(Array.Empty<WorkflowDelivery>());
        public Task LockOwnerAccountsAsync(Guid userId, CancellationToken ct) => Task.CompletedTask;
        public Task<bool> HasPendingOwnerAccountAsync(Guid userId, CancellationToken ct) => Task.FromResult(false);
        public Task EnsureOwnerTenantAsync(Guid id, string name, string email, string phone, CancellationToken ct) => Task.CompletedTask;
        public Task<List<UserLoginActivity>> ReadLoginActivityAsync(Guid id, CancellationToken ct) => Task.FromResult(new List<UserLoginActivity>());
        public Task<User?> LockOptionalUserAsync(Guid id, CancellationToken ct) => Task.FromResult<User?>(null);
        public Task<int> RevokeActiveSessionsAsync(Guid id, CancellationToken ct) => Task.FromResult(0);
        public Task<(List<User> Items, int Total)> SearchUsersAsync(UserSearchDto query, CancellationToken ct) => Task.FromResult((new List<User>(), 0));
        public Task<User?> FindManagedUserAsync(Guid id, CancellationToken ct) => Task.FromResult<User?>(null);
        public Task LoadUserRolesAsync(User user, CancellationToken ct) => Task.CompletedTask;
        public Task<List<Guid>> LockPermissionRowsAsync(Guid id, string permission, CancellationToken ct) => Task.FromResult(new List<Guid>());
        public Task<Guid[]> ReadOwnerTenantsAsync(Guid actor, string permission, CancellationToken ct) => Task.FromResult(Array.Empty<Guid>());
        public Guid PendingOtpRecipient(string destination) => Guid.NewGuid();
        public Task<int> CancelDriverOtpDeliveriesAsync(Guid userId, CancellationToken ct) => Task.FromResult(0);
        public void RemoveCredential(MfaCredential credential) { }
        public bool IsNewCredential(MfaCredential credential) => true;
        public void ResetTracking() { }
        public void ForgetRecoveryCodes() { }
    }

    private sealed class FakeIdentityAccessRepo : IIdentityAccessRepository
    {
        public readonly Dictionary<Guid, (User User, AuthChallenge Challenge)> Invitations = new();

        public Task<(User User, AuthChallenge Challenge)?> LockInvitationAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult(Invitations.TryGetValue(id, out var val) ? ((User, AuthChallenge)?)val : null);

        public Task RevokeSessionsAsync(Guid userId, CancellationToken cancellationToken) => Task.CompletedTask;
        public void AddOperator(User user, IEnumerable<OperatorGrant> grants, AuthChallenge challenge, WorkflowDelivery delivery) { }
        public Task<IReadOnlyList<AuthChallenge>> GetPendingInvitationsAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AuthChallenge>>(Array.Empty<AuthChallenge>());
        public Task ReplaceInvitationAsync(Guid userId, AuthChallenge challenge, WorkflowDelivery delivery, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Guid[]> OwnerTenantsAsync(Guid ownerId, string permission, CancellationToken ct) => Task.FromResult(Array.Empty<Guid>());
        public Task<IReadOnlyList<OperatorDto>> ListOperatorsAsync(Guid actor, Guid[] tenants, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OperatorDto>>(Array.Empty<OperatorDto>());
        public Task<IReadOnlyList<OperatorAssignment>> AssignmentsAsync(Guid actor, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OperatorAssignment>>(Array.Empty<OperatorAssignment>());
        public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<User?> GetOperatorForUpdateAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult<User?>(null);
        public Task<IReadOnlyList<OperatorGrant>> GetOperatorGrantsAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OperatorGrant>>(Array.Empty<OperatorGrant>());
    }

    [Fact]
    public async Task Driver_RequiresSimulation_DeclinedThrows_ConsentPersistsGuid()
    {
        var store = new FakeWorkflowStore();
        var repo = new FakeIdentityAccessRepo();
        var clock = new TestClock();
        var service = new VNeIdSimulationService(store, repo, clock);

        var userId = Guid.NewGuid();
        var regId = Guid.NewGuid();
        var user = new User { Id = userId, FullName = "Nguyen Van A", Status = UserStatus.PendingVerification };
        var reg = new DriverRegistration { Id = regId, UserId = userId };
        store.Stage(user);
        store.Stage(reg);

        // 1. Consent false throws
        var exConsent = await Assert.ThrowsAsync<AuthException>(() =>
            service.CompleteDriverSimulationAsync(new SimulatedVNeIdRequest(regId, false), default));
        Assert.Equal("VNEID_CONSENT_REQUIRED", exConsent.Code);

        // 2. Consent true persists simulation GUID
        var res = await service.CompleteDriverSimulationAsync(new SimulatedVNeIdRequest(regId, true, "v1.1-test"), default);
        Assert.NotEqual(Guid.Empty, res.SimulatedVNeIdGuid);
        Assert.Equal("COMPLETED", res.SimulationStatus);

        var persisted = await store.FindSimulatedIdentityAsync(userId, default);
        Assert.NotNull(persisted);
        Assert.Equal(res.SimulatedVNeIdGuid, persisted.SimulatedVNeIdGuid);
        Assert.True(persisted.ConsentGiven);

        // 3. Replay returns same GUID (Idempotency)
        var replay = await service.CompleteDriverSimulationAsync(new SimulatedVNeIdRequest(regId, true), default);
        Assert.Equal(res.SimulatedVNeIdGuid, replay.SimulatedVNeIdGuid);
    }

    [Fact]
    public async Task Owner_SimulationPersistsAndSurvivesRetrieval()
    {
        var store = new FakeWorkflowStore();
        var repo = new FakeIdentityAccessRepo();
        var clock = new TestClock();
        var service = new VNeIdSimulationService(store, repo, clock);

        var userId = Guid.NewGuid();
        var appId = Guid.NewGuid();
        var user = new User { Id = userId, FullName = "Tran Owner", Status = UserStatus.PendingVerification };
        var app = new OwnerApplication { Id = appId, UserId = userId, Status = "pending" };
        store.Stage(user);
        store.Stage(app);

        // Check initial status is false
        var initialStatus = await service.GetStatusByUserIdAsync(userId, default);
        Assert.False(initialStatus.IsCompleted);

        // Complete simulation
        var res = await service.CompleteOwnerSimulationAsync(new SimulatedVNeIdRequest(appId, true), default);
        Assert.NotEqual(Guid.Empty, res.SimulatedVNeIdGuid);

        // Check status returns completed with GUID
        var completedStatus = await service.GetStatusByUserIdAsync(userId, default);
        Assert.True(completedStatus.IsCompleted);
        Assert.Equal(res.SimulatedVNeIdGuid, completedStatus.SimulatedVNeIdGuid);
        Assert.True(await service.HasCompletedSimulationAsync(userId, default));
    }

    [Fact]
    public async Task Operator_RequiresSimulation_CompletesWithGuid()
    {
        var store = new FakeWorkflowStore();
        var repo = new FakeIdentityAccessRepo();
        var clock = new TestClock();
        var service = new VNeIdSimulationService(store, repo, clock);

        var opUserId = Guid.NewGuid();
        var challengeId = Guid.NewGuid();
        var opUser = new User
        {
            Id = opUserId,
            Email = "op@test.com",
            FullName = "Le Operator",
            Status = UserStatus.PendingVerification,
            Accounts = [new Account { RoleCode = "SITE_OPERATOR", Status = "PENDING_VERIFICATION" }]
        };
        var challenge = new AuthChallenge
        {
            Id = challengeId,
            UserId = opUserId,
            Purpose = "BOOTSTRAP_PASSWORD",
            Channel = "email",
            Destination = "op@test.com",
            ExpiresAt = clock.GetUtcNow().AddHours(1)
        };
        repo.Invitations[challengeId] = (opUser, challenge);
        store.Stage(opUser);

        // Complete operator simulation
        var res = await service.CompleteOperatorSimulationAsync(challengeId, true, "v1.1-test", default);
        Assert.NotEqual(Guid.Empty, res.SimulatedVNeIdGuid);
        Assert.Equal("COMPLETED", res.SimulationStatus);

        var persisted = await store.FindSimulatedIdentityAsync(opUserId, default);
        Assert.NotNull(persisted);
        Assert.Equal(res.SimulatedVNeIdGuid, persisted.SimulatedVNeIdGuid);
    }
}
