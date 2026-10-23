using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.DTOs;
namespace UserService.Persistence;
public partial class AppDbContext : IIdentityWorkflowStore
{
    private AppDbContext db => this;
    Guid IIdentityWorkflowStore.PendingOtpRecipient(string destination) => Users.Local.Single(u => u.DeletedOn == null && u.Status == UserStatus.PendingVerification && (u.Email == destination || u.Phone == destination)).Id!.Value;
    Task<int> IIdentityWorkflowStore.CancelDriverOtpDeliveriesAsync(Guid userId, CancellationToken ct) => WorkflowDeliveries.Where(d => d.UserId == userId && d.Kind == "DRIVER_OTP" && d.Status != "sent").ExecuteUpdateAsync(s => s.SetProperty(d => d.Status, "cancelled").SetProperty(d => d.ProtectedPayload, ""), ct);
    Task<int> IIdentityWorkflowStore.CancelChallengeDeliveriesAsync(Guid previous, CancellationToken ct) =>
        db.WorkflowDeliveries.Where(d=>d.ChallengeId==previous && d.Status!="sent").ExecuteUpdateAsync(s=>s.SetProperty(d=>d.Status,"cancelled").SetProperty(d=>d.ProtectedPayload,""),ct);
    Task<User?> IIdentityWorkflowStore.LockPendingOwnerByContactAsync(string contact, CancellationToken ct) =>
        db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE deleted_at IS NULL AND status IN ('PENDING_VERIFICATION','PENDING_APPROVAL') AND (email={contact} OR phone={contact}) FOR UPDATE").SingleOrDefaultAsync(ct);
    Task<OwnerApplication?> IIdentityWorkflowStore.FindOwnerApplicationByUserAsync(Guid? userId, CancellationToken ct) =>
        db.OwnerApplications.AsNoTracking().SingleOrDefaultAsync(a => a.UserId == userId, ct);
    Task<AuthChallenge?> IIdentityWorkflowStore.FindOwnerContactAsync(Guid? userId, CancellationToken ct) =>
        db.AuthChallenges.SingleOrDefaultAsync(c => c.UserId == userId && c.Purpose == "OWNER_CONTACT" && c.Channel == "email", ct);
    Task<OwnerApplication?> IIdentityWorkflowStore.FindOwnerApplicationByEmailAsync(string email, CancellationToken ct) =>
        db.OwnerApplications.AsNoTracking().SingleOrDefaultAsync(a => a.User.Email == email && a.User.DeletedOn == null, ct);
    Task<AuthChallenge?> IIdentityWorkflowStore.FindOwnerSetupRecoveryChallengeAsync(Guid userId, CancellationToken ct) =>
        db.AuthChallenges.SingleOrDefaultAsync(c => c.UserId == userId && c.Purpose == "OWNER_SETUP_RECOVERY", ct);
    Task<bool> IIdentityWorkflowStore.HasOwnerSetupAsync(Guid userId, CancellationToken ct) =>
        db.OwnerApplications.AsNoTracking().AnyAsync(a => a.UserId == userId && a.LotSetupJson != null, ct);
    Task<User?> IIdentityWorkflowStore.LockLoginUserByContactAsync(string contact, CancellationToken ct) =>
        db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE deleted_at IS NULL AND (lower(email)={contact} OR phone={contact}) FOR UPDATE").Include(u=>u.Accounts).ThenInclude(a=>a.AccountRoles).SingleOrDefaultAsync(ct);
    Task<AuthChallenge?> IIdentityWorkflowStore.FindOutstandingLoginAsync(Guid? userId, CancellationToken ct) =>
        db.AuthChallenges.Where(c=>c.UserId==userId && c.Purpose=="LOGIN" && c.ConsumedAt==null).OrderByDescending(c=>c.ExpiresAt).FirstOrDefaultAsync(ct);
    Task<Guid?> IIdentityWorkflowStore.FindChallengeUserIdAsync(Guid id, CancellationToken ct) =>
        db.AuthChallenges.AsNoTracking().Where(c=>c.Id==id).Select(c=>(Guid?)c.UserId).SingleOrDefaultAsync(ct);
    Task<User> IIdentityWorkflowStore.LockUserAsync(Guid id, CancellationToken ct) =>
        db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id={id} FOR UPDATE").SingleAsync(ct);
    Task<AuthChallenge?> IIdentityWorkflowStore.LockChallengeAsync(Guid id, CancellationToken ct) =>
        db.AuthChallenges.FromSqlInterpolated($"SELECT * FROM auth_challenges WHERE id={id} FOR UPDATE").SingleOrDefaultAsync(ct);
    Task<int> IIdentityWorkflowStore.CloseOwnerPhoneChallengesAsync(Guid userId, DateTimeOffset now, CancellationToken ct) =>
        db.AuthChallenges.Where(c=>c.UserId==userId && c.Purpose=="OWNER_PHONE" && c.ConsumedAt==null)
            .ExecuteUpdateAsync(s=>s.SetProperty(c=>c.ConsumedAt,now).SetProperty(c=>c.CodeHash,""),ct);
    Task<int> IIdentityWorkflowStore.CancelOwnerPhoneDeliveriesAsync(Guid userId, CancellationToken ct) =>
        db.WorkflowDeliveries.Where(d=>d.UserId==userId && d.Kind=="OWNER_PHONE" && (d.Status=="pending" || d.Status=="failed"))
            .ExecuteUpdateAsync(s=>s.SetProperty(d=>d.Status,"cancelled").SetProperty(d=>d.ProtectedPayload,""),ct);
    Task<User> IIdentityWorkflowStore.LockUserWithRolesAsync(Guid id, CancellationToken ct) =>
        db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id={id} FOR UPDATE").Include(u=>u.Accounts).ThenInclude(a=>a.AccountRoles).SingleAsync(ct);
    Task<MfaCredential?> IIdentityWorkflowStore.LockMfaCredentialAsync(Guid id, CancellationToken ct) =>
        db.MfaCredentials.FromSqlInterpolated($"SELECT * FROM mfa_credentials WHERE user_id={id} FOR UPDATE").SingleOrDefaultAsync(ct);
    Task<WorkflowDelivery?> IIdentityWorkflowStore.LockDeliveryAsync(Guid id, CancellationToken ct) =>
        db.WorkflowDeliveries.FromSqlInterpolated($"SELECT * FROM workflow_deliveries WHERE id={id} FOR UPDATE").SingleOrDefaultAsync(ct);
    Task<Guid?[]> IIdentityWorkflowStore.ReadInvitationTenantsAsync(Guid userId, CancellationToken ct) =>
        db.OperatorGrants.AsNoTracking().Where(g => g.Account.UserId == userId && g.Account.DeletedOn == null)
                .Select(g => g.Account.TenantId).ToArrayAsync(ct);
    Task<List<WorkflowDelivery>> IIdentityWorkflowStore.LockDueDeliveryAsync(DateTimeOffset now, CancellationToken ct) =>
        db.WorkflowDeliveries.FromSqlInterpolated($"SELECT * FROM workflow_deliveries WHERE status IN ('pending','failed') AND (retry_at IS NULL OR retry_at<={now}) ORDER BY created_at LIMIT 1 FOR UPDATE SKIP LOCKED").ToListAsync(ct);
    ValueTask<AuthChallenge?> IIdentityWorkflowStore.FindChallengeAsync(Guid challengeId, CancellationToken ct) =>
        db.AuthChallenges.FindAsync([challengeId],ct);
    Task<bool> IIdentityWorkflowStore.IsMfaEnabledAsync(Guid id, CancellationToken ct) =>
        db.MfaCredentials.AnyAsync(credential => credential.UserId == id && credential.Enabled, ct);
    Task<int> IIdentityWorkflowStore.CountUnusedRecoveryCodesAsync(Guid id, CancellationToken ct) =>
        db.MfaRecoveryCodes.CountAsync(code => code.UserId == id && code.UsedAt == null, ct);
    Task<int> IIdentityWorkflowStore.DeleteRecoveryCodesAsync(Guid id, CancellationToken ct) =>
        db.MfaRecoveryCodes.Where(code => code.UserId == id).ExecuteDeleteAsync(ct);
    Task<MfaRecoveryCode?> IIdentityWorkflowStore.FindUnusedRecoveryCodeAsync(Guid userId, string hash, CancellationToken ct) =>
        db.MfaRecoveryCodes.SingleOrDefaultAsync(
                code => code.UserId == userId && code.CodeHash == hash && code.UsedAt == null, ct);
    Task<int> IIdentityWorkflowStore.CloseLoginChallengesAsync(Guid id, DateTimeOffset now, CancellationToken ct) =>
        db.AuthChallenges.Where(challenge => challenge.UserId == id && challenge.ConsumedAt == null &&
                (challenge.Purpose == "PASSWORD_MFA" || challenge.Purpose == "LOGIN"))
            .ExecuteUpdateAsync(update => update.SetProperty(challenge => challenge.ConsumedAt, now)
                .SetProperty(challenge => challenge.CodeHash, ""), ct);
    Task<int> IIdentityWorkflowStore.RevokeMfaSessionsAsync(Guid id, CancellationToken ct) =>
        db.AuthSessions.Where(session => session.UserId == id)
            .ExecuteUpdateAsync(update => update.SetProperty(session => session.IsRevoked, true), ct);
    Task<bool> IIdentityWorkflowStore.ContactExistsAsync(string? email, string? phone, CancellationToken ct) =>
        db.Users.AnyAsync(u => u.DeletedOn == null &&
            ((email != null && u.Email != null && u.Email.ToLower() == email) || (phone != null && u.Phone == phone)), ct);
    Task<User?> IIdentityWorkflowStore.FindPendingDriverAsync(string contact, CancellationToken ct) =>
        db.Users.SingleOrDefaultAsync(u=>u.DeletedOn==null && u.Status==UserStatus.PendingVerification && (u.Email==contact || u.Phone==contact),ct);
    Task<DriverRegistration?> IIdentityWorkflowStore.FindPendingDriverRegistrationAsync(Guid? userId, CancellationToken ct) =>
        db.DriverRegistrations.SingleOrDefaultAsync(r=>r.UserId==userId && r.VerifiedAt==null,ct);
    Task<DriverRegistration?> IIdentityWorkflowStore.LockDriverRegistrationAsync(Guid id, CancellationToken ct) =>
        db.DriverRegistrations.FromSqlInterpolated(
            $"SELECT * FROM driver_registrations WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
    Task<bool> IIdentityWorkflowStore.OwnerContactExistsAsync(string email, string phone, CancellationToken ct) =>
        db.Users.AnyAsync(u => u.DeletedOn == null &&
            ((u.Email != null && u.Email.ToLower() == email) || u.Phone == phone), ct);
    Task<OwnerApplication?> IIdentityWorkflowStore.LockOwnerApplicationAsync(Guid id, CancellationToken ct) =>
        db.OwnerApplications.FromSqlInterpolated(
            $"SELECT * FROM owner_applications WHERE id={id} FOR UPDATE").SingleOrDefaultAsync(ct);
    Task<AuthChallenge> IIdentityWorkflowStore.GetOwnerContactAsync(Guid userId, CancellationToken ct) =>
        db.AuthChallenges.SingleAsync(c => c.UserId == userId && c.Purpose == "OWNER_CONTACT" && c.Channel == "email", ct);
    Task<bool> IIdentityWorkflowStore.IsActiveAdminAsync(Guid id, CancellationToken ct) =>
        db.Users.AnyAsync(u => u.Id == id && u.DeletedOn == null && u.Status == UserStatus.Active &&
            u.Accounts.Any(a => a.DeletedOn == null && a.Status == "ACTIVE" &&
                a.AccountRoles.Any(r => r.RoleCode == "PLATFORM_ADMIN" || r.RoleCode == "ADMIN")), ct);
    Task<List<OwnerApplication>> IIdentityWorkflowStore.ListOwnerApplicationsAsync(CancellationToken ct) =>
        db.OwnerApplications.AsNoTracking().Include(a => a.User)
            .Where(a => a.User.DeletedOn == null).OrderByDescending(a => a.SubmittedAt).ToListAsync(ct);
    Task<Guid[]> IIdentityWorkflowStore.ReadVerifiedUserIdsAsync(CancellationToken ct) =>
        db.Users.Where(u=>u.EmailVerifiedAt!=null).Select(u=>u.Id!.Value).ToArrayAsync(ct);
    Task<WorkflowDelivery[]> IIdentityWorkflowStore.ReadOwnerDecisionDeliveriesAsync(CancellationToken ct) =>
        db.WorkflowDeliveries.AsNoTracking().Where(d=>d.Kind=="OWNER_DECISION").ToArrayAsync(ct);
    Task IIdentityWorkflowStore.LockOwnerAccountsAsync(Guid userId, CancellationToken ct) =>
        db.Accounts.FromSqlInterpolated($"SELECT * FROM accounts WHERE user_id={userId} ORDER BY id FOR UPDATE").Include(a=>a.AccountRoles).LoadAsync(ct);
    Task<bool> IIdentityWorkflowStore.HasPendingOwnerAccountAsync(Guid userId, CancellationToken ct) =>
        db.Accounts.AnyAsync(a => a.UserId == userId && a.DeletedOn == null && a.Status == "PENDING_APPROVAL" && a.AccountRoles.Any(r => r.RoleCode == "BUSINESS_OWNER"), ct);
    async Task IIdentityWorkflowStore.EnsureOwnerTenantAsync(Guid id, string name, string email, string phone, CancellationToken ct)
    {
        if (Database.CurrentTransaction is null)
            throw new InvalidOperationException("Owner tenant creation requires the approval transaction.");
        var code = "T-" + id.ToString("N");
        await Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO tenants(id,code,name,contact_email,contact_phone)
            VALUES ({id},{code},{name},{email},{phone}) ON CONFLICT DO NOTHING
            """, ct);
        // Protect an existing tenant from concurrent changes until activation commits.
        // A retry may reuse matching data, but must never overwrite or reactivate it.
        var matches = await Database.SqlQuery<bool>($"""
            SELECT (name={name} AND contact_email={email} AND contact_phone={phone}
                AND status='ACTIVE' AND deleted_at IS NULL) AS "Value"
            FROM tenants WHERE id={id} FOR UPDATE
            """).ToListAsync(ct);
        if (matches.Count != 1 || !matches[0])
            throw new AuthException("APPROVAL_CONFLICT", "Tenant provisioning conflicts with an existing tenant.", 409);
    }
    Task<List<UserLoginActivity>> IIdentityWorkflowStore.ReadLoginActivityAsync(Guid id, CancellationToken ct) =>
        db.AuthSessions.AsNoTracking().Where(s => s.UserId == id).OrderByDescending(s => s.CreatedAt)
            .Take(50).Select(s => new UserLoginActivity(s.CreatedAt, s.ExpiresAt, s.IsRevoked)).ToListAsync(ct);
    Task<User?> IIdentityWorkflowStore.LockOptionalUserAsync(Guid id, CancellationToken ct) =>
        db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
    Task<int> IIdentityWorkflowStore.RevokeActiveSessionsAsync(Guid id, CancellationToken ct) =>
        db.AuthSessions.Where(s => s.UserId == id && !s.IsRevoked)
                .ExecuteUpdateAsync(s => s.SetProperty(s => s.IsRevoked, true), ct);
    private IQueryable<User> ManagedUsers => db.Users.Where(u => u.DeletedOn == null).Include(u => u.Accounts).ThenInclude(a => a.AccountRoles);
    async Task<(List<User> Items, int Total)> IIdentityWorkflowStore.SearchUsersAsync(UserSearchDto query, CancellationToken ct)
    {
        var users = ManagedUsers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            users = users.Where(u => u.FullName.ToLower().Contains(search) ||
                (u.Email != null && u.Email.ToLower().Contains(search)) || (u.Phone != null && u.Phone.Contains(search)));
        }
        if (query.Status != null)
        {
            var status = Enum.Parse<UserStatus>(query.Status, true);
            users = users.Where(u => u.Status == status);
        }
        if (query.Role != null)
        {
            string[] codes = query.Role switch { "admin" => ["PLATFORM_ADMIN", "ADMIN"],
                "owner" => ["BUSINESS_OWNER", "OWNER"], "operator" => ["SITE_OPERATOR", "OPERATOR"], _ => ["DRIVER"] };
            users = users.Where(u => u.Accounts.Any(a => a.DeletedOn == null && a.AccountRoles.Any(r => codes.Contains(r.RoleCode))));
        }
        var total = await users.CountAsync(ct);
        var offset = (long)(query.Page - 1) * query.PageSize;
        var items = offset > int.MaxValue ? new List<User>() : await users.OrderBy(u => u.FullName).ThenBy(u => u.Id)
            .Skip((int)offset).Take(query.PageSize).ToListAsync(ct);
        return (items, total);
    }
    Task<User?> IIdentityWorkflowStore.FindManagedUserAsync(Guid id, CancellationToken ct) => ManagedUsers.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, ct);
    Task IIdentityWorkflowStore.LoadUserRolesAsync(User user, CancellationToken ct) => db.Entry(user).Collection(u => u.Accounts).Query().Include(a => a.AccountRoles).LoadAsync(ct);
    async Task<List<Guid>> IIdentityWorkflowStore.LockPermissionRowsAsync(Guid id, string permission, CancellationToken ct)
    {
        return await db.Database.SqlQuery<Guid>($"""
            SELECT a.id AS "Value" FROM accounts a JOIN users u ON u.id=a.user_id
            JOIN account_roles r ON r.account_id=a.id
            WHERE u.id={id} AND u.status='ACTIVE' AND u.deleted_at IS NULL AND a.status='ACTIVE' AND a.deleted_at IS NULL
            AND (({permission}='ACCOUNT_ADMIN' AND r.role_code IN ('ADMIN','PLATFORM_ADMIN'))
              OR ({permission} IN ('OPERATOR_MANAGE','PARKING_MANAGE','BACKUP_CONFIGURE','BACKUP_MARK') AND r.role_code='BUSINESS_OWNER'))
            AND ((a.permissions IS NULL AND {permission}<>'BACKUP_MARK') OR {permission}=ANY(a.permissions))
            ORDER BY a.id FOR SHARE OF u,a,r
            """).ToListAsync(ct);
    }
    Task<Guid[]> IIdentityWorkflowStore.ReadOwnerTenantsAsync(Guid actor, string permission, CancellationToken ct) => new Repositories.IdentityAccessRepository(db).OwnerTenantsAsync(actor, permission, ct);
    async Task<IUnitOfWorkTransaction> IIdentityWorkflowStore.BeginTransactionAsync(CancellationToken ct) => new WorkflowTransaction(await Database.BeginTransactionAsync(ct));
    Task IIdentityWorkflowStore.CommitCurrentTransactionAsync(CancellationToken ct) => Database.CurrentTransaction!.CommitAsync(ct);
    void IIdentityWorkflowStore.Stage<T>(T entity) => Set<T>().Add(entity);
    void IIdentityWorkflowStore.RemoveCredential(MfaCredential credential) => MfaCredentials.Remove(credential);
    bool IIdentityWorkflowStore.IsNewCredential(MfaCredential credential) => Entry(credential).State == EntityState.Detached;
    void IIdentityWorkflowStore.ResetTracking() => ChangeTracker.Clear();
    void IIdentityWorkflowStore.ForgetRecoveryCodes()
    {
        foreach (var entry in ChangeTracker.Entries<MfaRecoveryCode>().ToArray()) entry.State = EntityState.Detached;
    }
    async Task IIdentityWorkflowStore.SaveRegistrationAsync(CancellationToken ct)
    {
        try { await SaveChangesAsync(ct); }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: "23505" })
        { throw new AuthException("CONTACT_EXISTS", "Email or phone is already registered.", 409); }
    }
    private sealed class WorkflowTransaction(IDbContextTransaction transaction) : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken ct) => transaction.CommitAsync(ct);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
