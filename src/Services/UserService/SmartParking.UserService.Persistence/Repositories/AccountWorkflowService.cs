using System.ComponentModel.DataAnnotations;
using UserService.Application.Common.Validation;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.DTOs;
using UserService.Application.Services;
using UserService.Application.Common.Models.JwT;

namespace UserService.Persistence.Repositories;

public sealed partial class AccountWorkflowService(AppDbContext db, IPasswordService passwords, IWorkflowProtector protector,
    TimeProvider clock, AuthenticationPolicy? authenticationPolicy = null) : IMfaPolicy,IAccountWorkflows
{
    private static AuthException Error(string code, string message, int status=400) => new(code,message,status);
    public static void Validate(object body)
    {
        var errors=new List<ValidationResult>();
        if(!Validator.TryValidateObject(body,new(body),errors,true)) throw Error("VALIDATION_FAILED",string.Join(" ",errors.Select(e=>e.ErrorMessage)));
    }
    public static string Contact(string value)
    {
        if (!SignInContact.IsValid(value)) throw Error("VALIDATION_FAILED",SignInContact.Message);
        value=value.Trim();
        if(value.Contains('@')) return value.ToLowerInvariant();
        if(value.StartsWith("+84")) return "0"+value[3..];
        if(value.StartsWith("84") && value.Length==11) return "0"+value[2..];
        return value;
    }
    public static async Task RequirePermissionAsync(AppDbContext db, Guid id, string permission, CancellationToken ct)
    {
        // Shared locks remain held by a caller's transaction until the action commits.
        var rows=await db.Database.SqlQuery<Guid>($"""
            SELECT a.id AS "Value" FROM accounts a JOIN users u ON u.id=a.user_id
            JOIN account_roles r ON r.account_id=a.id
            WHERE u.id={id} AND u.status='ACTIVE' AND u.deleted_at IS NULL AND a.status='ACTIVE' AND a.deleted_at IS NULL
            AND (({permission}='ACCOUNT_ADMIN' AND r.role_code IN ('ADMIN','PLATFORM_ADMIN'))
              OR ({permission} IN ('OPERATOR_MANAGE','PARKING_MANAGE','BACKUP_CONFIGURE','BACKUP_MARK') AND r.role_code='BUSINESS_OWNER'))
            AND ((a.permissions IS NULL AND {permission}<>'BACKUP_MARK') OR {permission}=ANY(a.permissions))
            ORDER BY a.id FOR SHARE OF u,a,r
            """).ToListAsync(ct);
        if(rows.Count==0) throw Error("FORBIDDEN","Current action permission is required.",403);
    }
    public async Task<Guid> QueueAsync(Guid userId, Guid actorId, string kind, WorkflowMessage message, CancellationToken ct, Guid? challengeId=null)
    {
        if(challengeId is {} previous)
            await db.WorkflowDeliveries.Where(d=>d.ChallengeId==previous && d.Status!="sent").ExecuteUpdateAsync(s=>s.SetProperty(d=>d.Status,"cancelled").SetProperty(d=>d.ProtectedPayload,""),ct);
        var delivery=new WorkflowDelivery { Id=Guid.NewGuid(),UserId=userId,ActorId=actorId,Kind=kind,
            ProtectedPayload=protector.Protect(JsonSerializer.Serialize(message)),CreatedAt=clock.GetUtcNow(),ChallengeId=challengeId,
            ExpiresAt=kind=="DRIVER_OTP"?clock.GetUtcNow().AddMinutes(5):null };
        db.WorkflowDeliveries.Add(delivery);
        await db.SaveChangesAsync(ct);
        return delivery.Id;
    }
    private async Task<AuthChallenge> IssueAsync(User user, string purpose, string channel, string destination, CancellationToken ct)
    {
        var code=RandomNumberGenerator.GetInt32(1_000_000).ToString("D6");
        var challenge=new AuthChallenge { Id=Guid.NewGuid(),UserId=user.Id!.Value,Purpose=purpose,Channel=channel,Destination=destination,
            CodeHash=passwords.Hash(code),ExpiresAt=clock.GetUtcNow().AddMinutes(5),ResendAt=clock.GetUtcNow().AddMinutes(1) };
        db.AuthChallenges.Add(challenge); await db.SaveChangesAsync(ct);
        await QueueAsync(user.Id.Value,user.Id.Value,purpose,OtpWorkflowMessage.Create(channel,destination,code),ct,challenge.Id);
        return challenge;
    }
    public static ChallengeResult Result(AuthChallenge c)=>new(c.Id,c.ExpiresAt,c.ResendAt);
    public async Task<ChallengeResult> IssueOwnerAsync(User user,CancellationToken ct) => Result(await IssueAsync(user,"OWNER_CONTACT","email",user.Email!,ct));
    public async Task<object> RecoverOwnerAsync(string contact,CancellationToken ct)
    {
        contact=Contact(contact);
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var user=await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE deleted_at IS NULL AND status IN ('PENDING_VERIFICATION','PENDING_APPROVAL') AND (email={contact} OR phone={contact}) FOR UPDATE").SingleOrDefaultAsync(ct)
            ??throw Error("REGISTRATION_NOT_FOUND","Pending registration was not found.",404);
        var challenge=await db.AuthChallenges.SingleOrDefaultAsync(c=>c.UserId==user.Id && c.Purpose=="OWNER_CONTACT" && c.Channel=="email",ct)
            ??throw Error("REGISTRATION_NOT_FOUND","Pending Owner registration was not found.",404);
        // Recover registrations created under the old two-contact gate.
        if(user.EmailVerifiedAt is not null && user.Status==UserStatus.PendingVerification)
        {
            user.Status=UserStatus.PendingApproval;
            user.ModifiedOn=clock.GetUtcNow();
        }
        await CancelOwnerPhoneAsync(user.Id!.Value,ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new {verification=Result(challenge),emailVerified=user.EmailVerifiedAt!=null,phoneVerified=user.PhoneVerifiedAt!=null};
    }
    public async Task<ChallengeResult> RequestLoginAsync(string contact,CancellationToken ct)
    {
        contact=Contact(contact);
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var user=await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE deleted_at IS NULL AND (lower(email)={contact} OR phone={contact}) FOR UPDATE").Include(u=>u.Accounts).ThenInclude(a=>a.AccountRoles).SingleOrDefaultAsync(ct);
        if(user?.Status==UserStatus.Locked && user.LockedUntil is {} until && until<=clock.GetUtcNow())
        {user.Status=UserStatus.Active;user.LockedUntil=null;user.FailedLoginAttempts=0;}
        if(user is null) throw Error("AUTH_FAILED","Account is not eligible for sign in.",401);
        if(user.Status!=UserStatus.Active || AccessTokenService.CurrentRole(user) is null) throw SignInErrors.ForStatus(user.Status);
        var role=AccessTokenService.CurrentRole(user);
        if(contact.Contains('@')?user.EmailVerifiedAt is null:user.PhoneVerifiedAt is null)
            throw Error("CONTACT_UNVERIFIED","Use a verified contact for OTP sign in.",401);
        // One outstanding login challenge per account: changing browser cannot reset counters.
        var previous=await db.AuthChallenges.Where(c=>c.UserId==user.Id && c.Purpose=="LOGIN" && c.ConsumedAt==null).OrderByDescending(c=>c.ExpiresAt).FirstOrDefaultAsync(ct);
        if(previous is not null)
        {
            if(previous.LockedUntil>clock.GetUtcNow()) throw Error("OTP_LOCKED","Try again after the OTP lock expires.",423);
            if(previous.ResendAt>clock.GetUtcNow()) throw Error("OTP_COOLDOWN","Wait before requesting another code.",429);
            previous.ConsumedAt=clock.GetUtcNow();
        }
        var challenge=await IssueAsync(user,"LOGIN",contact.Contains('@')?"email":"sms",contact,ct);
        if(previous is not null && previous.LockedUntil is null) challenge.Attempts=previous.Attempts;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Result(challenge);
    }
    private async Task<AuthChallenge> LoadAsync(Guid id,string purpose,CancellationToken ct)
    {
        // All account flows lock the user before challenges, including login issuance.
        var userId=await db.AuthChallenges.AsNoTracking().Where(c=>c.Id==id).Select(c=>(Guid?)c.UserId).SingleOrDefaultAsync(ct);
        if(userId is null)throw Error("CHALLENGE_CLOSED","Challenge is not available for this purpose.",409);
        await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id={userId.Value} FOR UPDATE").SingleAsync(ct);
        var c=await db.AuthChallenges.FromSqlInterpolated($"SELECT * FROM auth_challenges WHERE id={id} FOR UPDATE").SingleOrDefaultAsync(ct);
        if(c is null || c.Purpose!=purpose || c.ConsumedAt!=null) throw Error("CHALLENGE_CLOSED","Challenge is not available for this purpose.",409);
        if(c.LockedUntil>clock.GetUtcNow()) throw Error("OTP_LOCKED","Try again after 15 minutes.",423);
        if(c.LockedUntil!=null) {c.LockedUntil=null;c.Attempts=0;}
        return c;
    }
    private async Task<bool> CheckAsync(AuthChallenge c,string code,CancellationToken ct)
    {
        if(c.ExpiresAt<=clock.GetUtcNow()) throw Error("OTP_EXPIRED","Request a new code.");
        if(passwords.Verify(code,c.CodeHash)) return true;
        c.Attempts++; if(c.Attempts>=3)c.LockedUntil=clock.GetUtcNow().AddMinutes(15);
        await db.SaveChangesAsync(ct); return false;
    }
    public async Task VerifyOwnerAsync(Guid id,string code,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var c=await LoadAsync(id,"OWNER_CONTACT",ct);
        var user=await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id={c.UserId} FOR UPDATE").SingleAsync(ct);
        if(user.DeletedOn!=null || user.Status!=UserStatus.PendingVerification) throw Error("CHALLENGE_CLOSED","Account is no longer awaiting verification.",409);
        if(!await CheckAsync(c,code,ct)) {await tx.CommitAsync(ct);throw Error(c.LockedUntil!=null?"OTP_LOCKED":"OTP_INVALID","Invalid verification code.",c.LockedUntil!=null?423:400);}
        c.ConsumedAt=clock.GetUtcNow();c.CodeHash="";
        user.EmailVerifiedAt=clock.GetUtcNow();
        user.Status=UserStatus.PendingApproval;
        user.ModifiedOn=clock.GetUtcNow();
        await CancelOwnerPhoneAsync(user.Id!.Value,ct);
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
    }
    private async Task CancelOwnerPhoneAsync(Guid userId,CancellationToken ct)
    {
        await db.AuthChallenges.Where(c=>c.UserId==userId && c.Purpose=="OWNER_PHONE" && c.ConsumedAt==null)
            .ExecuteUpdateAsync(s=>s.SetProperty(c=>c.ConsumedAt,clock.GetUtcNow()).SetProperty(c=>c.CodeHash,""),ct);
        await db.WorkflowDeliveries.Where(d=>d.UserId==userId && d.Kind=="OWNER_PHONE" && (d.Status=="pending" || d.Status=="failed"))
            .ExecuteUpdateAsync(s=>s.SetProperty(d=>d.Status,"cancelled").SetProperty(d=>d.ProtectedPayload,""),ct);
    }
    public async Task<ChallengeResult> ResendOwnerAsync(Guid id,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);var c=await LoadAsync(id,"OWNER_CONTACT",ct);
        var user=await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id={c.UserId} FOR UPDATE").SingleAsync(ct);
        if(user.Status!=UserStatus.PendingVerification || user.DeletedOn!=null) throw Error("CHALLENGE_CLOSED","Account is no longer awaiting verification.",409);
        if(c.ResendAt>clock.GetUtcNow()) throw Error("OTP_COOLDOWN","Wait 60 seconds.",429);
        var code=RandomNumberGenerator.GetInt32(1_000_000).ToString("D6");
        while(passwords.Verify(code,c.CodeHash))code=RandomNumberGenerator.GetInt32(1_000_000).ToString("D6");
        c.CodeHash=passwords.Hash(code);c.ExpiresAt=clock.GetUtcNow().AddMinutes(5);c.ResendAt=clock.GetUtcNow().AddMinutes(1);
        await QueueAsync(c.UserId,c.UserId,"OWNER_CONTACT",OtpWorkflowMessage.Create(c.Channel,c.Destination,code),ct,c.Id);
        await tx.CommitAsync(ct);return Result(c);
    }
    public async Task<UserSessionDto> CompleteLoginAsync(CompleteOtpLoginDto body,AuthSessionService sessions,CancellationToken ct)
    {
        Validate(body);await using var tx=await db.Database.BeginTransactionAsync(ct);var c=await LoadAsync(body.ChallengeId,"LOGIN",ct);
        var user=await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id={c.UserId} FOR UPDATE").Include(u=>u.Accounts).ThenInclude(a=>a.AccountRoles).SingleAsync(ct);
        if(user.DeletedOn!=null)throw Error("AUTH_FAILED","Account access is blocked.",401);
        if(user.Status!=UserStatus.Active || AccessTokenService.CurrentRole(user) is null)throw SignInErrors.ForStatus(user.Status);
        if(!await CheckAsync(c,body.Code,ct)) {await tx.CommitAsync(ct);throw Error(c.LockedUntil!=null?"OTP_LOCKED":"OTP_INVALID","Invalid code.",c.LockedUntil!=null?423:400);}
        var mfa=await db.MfaCredentials.FromSqlInterpolated($"SELECT * FROM mfa_credentials WHERE user_id={c.UserId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (mfa?.Enabled == true)
        {
            await VerifyMfaAsync(mfa, body.Totp, body.RecoveryCode, ct);
        }
        c.ConsumedAt = clock.GetUtcNow();
        c.CodeHash = "";
        await db.SaveChangesAsync(ct);
        var result = await sessions.CreateAsync(user, ct);
        if (!string.IsNullOrWhiteSpace(body.RecoveryCode) && mfa?.Enabled == true)
            result.RecoveryCodesRemaining = await RemainingRecoveryCodesAsync(c.UserId, ct);
        await tx.CommitAsync(ct);
        return result;
    }
    public async Task<object> DeliveryAsync(Guid actor,Guid id,bool retry,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var d=await db.WorkflowDeliveries.FromSqlInterpolated($"SELECT * FROM workflow_deliveries WHERE id={id} FOR UPDATE").SingleOrDefaultAsync(ct);
        if(d is null || d.ActorId!=actor)throw Error("DELIVERY_NOT_FOUND","Delivery was not found.",404);
        if (d.Kind == "OPERATOR_ONBOARDING")
        {
            var tenants = await new IdentityAccessRepository(db).OwnerTenantsAsync(actor, "OPERATOR_MANAGE", ct);
            var targetTenants = await db.OperatorGrants.AsNoTracking().Where(g => g.Account.UserId == d.UserId && g.Account.DeletedOn == null)
                .Select(g => g.Account.TenantId).ToArrayAsync(ct);
            if (targetTenants.Length == 0 || targetTenants.Any(t => t is null || !tenants.Contains(t.Value)))
                throw Error("FORBIDDEN", "Current permission for every invitation tenant is required.", 403);
        }
        else await RequirePermissionAsync(db, actor, "ACCOUNT_ADMIN", ct);
        if(retry && d.Status=="failed") {d.Status="pending";d.RetryAt=null;await db.SaveChangesAsync(ct);}
        await tx.CommitAsync(ct);return new {d.Id,d.Kind,d.Status,d.Attempts,d.SentAt};
    }
    public async Task DeliverOneAsync(IWorkflowTransport transport,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var due=await db.WorkflowDeliveries.FromSqlInterpolated($"SELECT * FROM workflow_deliveries WHERE status IN ('pending','failed') AND (retry_at IS NULL OR retry_at<={clock.GetUtcNow()}) ORDER BY created_at LIMIT 1 FOR UPDATE SKIP LOCKED").ToListAsync(ct);
        if(due.Count==0)return;var d=due[0];
        if(d.ExpiresAt<=clock.GetUtcNow()){d.Status="cancelled";d.ProtectedPayload="";await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return;}
        if(d.ChallengeId is {} challengeId)
        {
            var challenge=await db.AuthChallenges.FindAsync([challengeId],ct);
            if(challenge is null || challenge.ConsumedAt!=null || challenge.ExpiresAt<=clock.GetUtcNow()) {d.Status="cancelled";d.ProtectedPayload="";await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return;}
        }
        d.Attempts++;
        try {await transport.SendAsync(JsonSerializer.Deserialize<WorkflowMessage>(protector.Unprotect(d.ProtectedPayload))!,d.Id,ct);d.Status="sent";d.SentAt=clock.GetUtcNow();d.ProtectedPayload="";}
        catch(Exception e) when(e is not OperationCanceledException || !ct.IsCancellationRequested) {d.Status="failed";d.RetryAt=clock.GetUtcNow().AddMinutes(Math.Min(60,d.Attempts*2));}
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
    }
}
