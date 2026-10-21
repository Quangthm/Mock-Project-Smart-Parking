using SmartParking.UserService.Domain.Entities;
using UserService.Application.DTOs;
namespace UserService.Application.Common.Interfaces.Persistence;
// Scoped unit of work. Lock methods require a local transaction; returned aggregates
// are tracked until commit/disposal. Approval provisioning runs outside this unit of work.
public interface IIdentityWorkflowStore
{
    Task<int> CancelChallengeDeliveriesAsync(Guid previous, CancellationToken ct);
    Task<User?> LockPendingOwnerByContactAsync(string contact, CancellationToken ct);
    Task<OwnerApplication?> FindOwnerApplicationByUserAsync(Guid? userId, CancellationToken ct);
    Task<AuthChallenge?> FindOwnerContactAsync(Guid? userId, CancellationToken ct);
    Task<bool> HasOwnerSetupAsync(Guid userId, CancellationToken ct);
    Task<User?> LockLoginUserByContactAsync(string contact, CancellationToken ct);
    Task<AuthChallenge?> FindOutstandingLoginAsync(Guid? userId, CancellationToken ct);
    Task<Guid?> FindChallengeUserIdAsync(Guid id, CancellationToken ct);
    Task<User> LockUserAsync(Guid id, CancellationToken ct);
    Task<AuthChallenge?> LockChallengeAsync(Guid id, CancellationToken ct);
    Task<int> CloseOwnerPhoneChallengesAsync(Guid userId, DateTimeOffset now, CancellationToken ct);
    Task<int> CancelOwnerPhoneDeliveriesAsync(Guid userId, CancellationToken ct);
    Task<User> LockUserWithRolesAsync(Guid id, CancellationToken ct);
    Task<MfaCredential?> LockMfaCredentialAsync(Guid id, CancellationToken ct);
    Task<WorkflowDelivery?> LockDeliveryAsync(Guid id, CancellationToken ct);
    Task<Guid?[]> ReadInvitationTenantsAsync(Guid userId, CancellationToken ct);
    Task<List<WorkflowDelivery>> LockDueDeliveryAsync(DateTimeOffset now, CancellationToken ct);
    ValueTask<AuthChallenge?> FindChallengeAsync(Guid challengeId, CancellationToken ct);
    Task<bool> IsMfaEnabledAsync(Guid id, CancellationToken ct);
    Task<int> CountUnusedRecoveryCodesAsync(Guid id, CancellationToken ct);
    Task<int> DeleteRecoveryCodesAsync(Guid id, CancellationToken ct);
    Task<MfaRecoveryCode?> FindUnusedRecoveryCodeAsync(Guid userId, string hash, CancellationToken ct);
    Task<int> CloseLoginChallengesAsync(Guid id, DateTimeOffset now, CancellationToken ct);
    Task<int> RevokeMfaSessionsAsync(Guid id, CancellationToken ct);
    Task<bool> ContactExistsAsync(string? email, string? phone, CancellationToken ct);
    Task<User?> FindPendingDriverAsync(string contact, CancellationToken ct);
    Task<DriverRegistration?> FindPendingDriverRegistrationAsync(Guid? userId, CancellationToken ct);
    Task<DriverRegistration?> LockDriverRegistrationAsync(Guid id, CancellationToken ct);
    Task<bool> OwnerContactExistsAsync(string email, string phone, CancellationToken ct);
    Task<OwnerApplication?> LockOwnerApplicationAsync(Guid id, CancellationToken ct);
    Task<AuthChallenge> GetOwnerContactAsync(Guid userId, CancellationToken ct);
    Task<bool> IsActiveAdminAsync(Guid id, CancellationToken ct);
    Task<List<OwnerApplication>> ListOwnerApplicationsAsync(CancellationToken ct);
    Task<Guid[]> ReadVerifiedUserIdsAsync(CancellationToken ct);
    Task<WorkflowDelivery[]> ReadOwnerDecisionDeliveriesAsync(CancellationToken ct);
    Task LockOwnerAccountsAsync(Guid userId, CancellationToken ct);
    Task<bool> HasPendingOwnerAccountAsync(Guid userId, CancellationToken ct);
    // Must run in the approval transaction so tenant creation rolls back with activation.
    Task EnsureOwnerTenantAsync(Guid id, string name, string email, string phone, CancellationToken ct);
    Task<List<UserLoginActivity>> ReadLoginActivityAsync(Guid id, CancellationToken ct);
    Task<User?> LockOptionalUserAsync(Guid id, CancellationToken ct);
    Task<int> RevokeActiveSessionsAsync(Guid id, CancellationToken ct);
    Task<(List<User> Items, int Total)> SearchUsersAsync(UserSearchDto query, CancellationToken ct);
    Task<User?> FindManagedUserAsync(Guid id, CancellationToken ct);
    Task LoadUserRolesAsync(User user, CancellationToken ct);
    Task<List<Guid>> LockPermissionRowsAsync(Guid id, string permission, CancellationToken ct);
    Task<Guid[]> ReadOwnerTenantsAsync(Guid actor, string permission, CancellationToken ct);
    Guid PendingOtpRecipient(string destination);
    Task<int> CancelDriverOtpDeliveriesAsync(Guid userId, CancellationToken ct);
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct);
    Task CommitCurrentTransactionAsync(CancellationToken ct);
    Task<int> SaveChangesAsync(CancellationToken ct);
    Task SaveRegistrationAsync(CancellationToken ct);
    void Stage<T>(T entity) where T : class;
    void RemoveCredential(MfaCredential credential);
    bool IsNewCredential(MfaCredential credential);
    void ResetTracking();
    void ForgetRecoveryCodes();
}
