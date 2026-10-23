using UserService.Application.Common.Interfaces.Persistence;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.DTOs;

namespace UserService.Application.Services;

public sealed class OwnerRegistrationService(IIdentityWorkflowStore db, IPasswordService passwords, TimeProvider clock, IParkingDirectory parking, AccountWorkflowService workflows)
    : IOwnerRegistrationService
{
    private static AuthException Error(string code, string message, int status = 400) => new(code, message, status);
    private static void Validate(object body)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(body, new ValidationContext(body), errors, true))
            throw Error("VALIDATION_FAILED", string.Join(" ", errors.Select(e => e.ErrorMessage)));
    }
    private static OwnerApplicationDto Result(OwnerApplication a) => new(a.Id, a.UserId, a.User.FullName,
        a.BusinessName, a.User.Email!, a.User.Phone!, a.LotType, a.Status, a.SubmittedAt,
        a.ReviewedAt, a.ReviewedBy, a.ReviewNote, ContactVerified: a.User.EmailVerifiedAt is not null,
        LotSetupComplete: a.LotSetupJson is not null,
        LotSetup: a.LotSetupJson is null ? null : JsonSerializer.Deserialize<OwnerLotSetupDto>(a.LotSetupJson));

    public async Task<OwnerApplicationDto> RegisterAsync(OwnerRegistrationDto body, CancellationToken ct)
    {
        Validate(body);
        var email = body.Email.Trim().ToLowerInvariant();
        var phone = AccountWorkflowService.Contact(body.Phone);
        await using var tx = await db.BeginTransactionAsync(ct);
        if (await db.OwnerContactExistsAsync(email, phone, ct))
            throw Error("CONTACT_EXISTS", "Email or phone is already registered.", 409);
        var now = clock.GetUtcNow();
        var user = new User { Id = Guid.NewGuid(), FullName = body.FullName.Trim(), CompanyName = body.BusinessName.Trim(), Email = email, Phone = phone,
            PasswordHash = string.IsNullOrEmpty(body.Password) ? "" : passwords.Hash(body.Password), Status = UserStatus.PendingVerification, CreatedOn = now, ModifiedOn = now };
        var account = new Account { Id = Guid.NewGuid(), User = user, UserId = user.Id.Value,
            Status = "PENDING_APPROVAL", CreatedOn = now };
        account.AccountRoles.Add(new AccountRole { Account = account, AccountId = account.Id.Value, RoleCode = "BUSINESS_OWNER" });
        user.Accounts.Add(account);
        var setupToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var application = new OwnerApplication { Id = Guid.NewGuid(), UserId = user.Id.Value, User = user,
            BusinessName = body.BusinessName.Trim(), LotType = body.LotType, SubmittedAt = now,
            SetupTokenHash = Hash(setupToken), SetupTokenExpiresAt = now.AddHours(24) };
        db.Stage(application);
        await db.SaveRegistrationAsync(ct);
        await tx.CommitAsync(ct);
        return Result(application) with { SetupToken = setupToken };
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public async Task<OwnerApplicationDto> SaveLotSetupAsync(OwnerLotSetupRequest body, CancellationToken ct)
    {
        Validate(body);
        Validate(body.Lot);
        await using var tx = await db.BeginTransactionAsync(ct);
        // Same lock order as review. Verification only reads the setup flag under the user lock.
        var application = await db.LockOwnerApplicationAsync(body.RegistrationId, ct);
        if (application?.SetupTokenHash is null || application.SetupTokenExpiresAt is null || application.SetupTokenExpiresAt <= clock.GetUtcNow() ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(application.SetupTokenHash), Encoding.UTF8.GetBytes(Hash(body.SetupToken))))
            throw Error("SETUP_TOKEN_INVALID", "Setup credential is invalid or expired. Verify your registered email to restore setup access.", 401);
        var user = await db.LockUserAsync(application.UserId, ct);
        application.User = user;
        if (application.Status != "pending" || application.ApprovalRequestedBy is not null || user.DeletedOn is not null || user.Status != UserStatus.PendingVerification)
            throw Error("APPLICATION_CLOSED", "Setup is no longer editable.", 409);
        // Serialize a typed payload for stable comparison, independent of incoming JSON property order.
        var json = JsonSerializer.Serialize(body.Lot);
        ChallengeResult challenge;
        if (application.LotSetupJson is not null)
        {
            var previous = JsonSerializer.Serialize(JsonSerializer.Deserialize<OwnerLotSetupDto>(application.LotSetupJson));
            if (previous != json) throw Error("SETUP_ALREADY_SAVED", "Setup has already been submitted.", 409);
            var existing = await db.GetOwnerContactAsync(application.UserId, ct);
            challenge = AccountWorkflowService.Result(existing);
        }
        else
        {
            application.LotSetupJson = json;
            application.LotType = body.Lot.LotType;
            await db.SaveChangesAsync(ct);
            challenge = await workflows.IssueOwnerAsync(user, ct);
        }
        await tx.CommitAsync(ct);
        return Result(application) with { Verification = challenge };
    }

    private async Task RequireAdmin(Guid id, CancellationToken ct)
    {
        await AccountWorkflowService.RequirePermissionAsync(db, id, "ACCOUNT_ADMIN", ct);
        if (!await db.IsActiveAdminAsync(id, ct))
            throw Error("FORBIDDEN", "Active Admin access is required.", 403);
    }

    public async Task<IReadOnlyList<OwnerApplicationDto>> ListAsync(Guid adminId, CancellationToken ct)
    {
        await RequireAdmin(adminId, ct);
        var applications = await db.ListOwnerApplicationsAsync(ct);
        var verified = await db.ReadVerifiedUserIdsAsync(ct);
        var deliveries=await db.ReadOwnerDecisionDeliveriesAsync(ct);
        return applications.Select(a => {var delivery=deliveries.SingleOrDefault(d=>d.UserId==a.UserId);return Result(a) with { ContactVerified = verified.Contains(a.UserId),DeliveryId=delivery?.Id,DeliveryStatus=delivery?.Status };}).ToList();
    }

    public async Task<OwnerApplicationDto> ReviewAsync(Guid adminId, Guid id, ReviewOwnerDto body, CancellationToken ct)
    {
        Validate(body);
        ApprovalIntent? intent = null;
        if (body.Status == "approved")
        {
            intent = await RecordApprovalIntentAsync(adminId, id, body, ct);
            // Durable intent precedes the remote effect. No local locks span HTTP.
            if (!intent.Completed)
                await parking.ProvisionOwnerSiteAsync(id, id, intent.BusinessName, intent.Email, intent.Phone,
                    JsonSerializer.Deserialize<OwnerLotSetupDto>(intent.LotJson)!, ct);
            db.ResetTracking();
        }
        await using var tx = await db.BeginTransactionAsync(ct);
        await RequireAdmin(adminId, ct);
        // Serialize competing reviews; only the first pending -> final decision may commit.
        var application = await db.LockOwnerApplicationAsync(id, ct);
        if (application is null) throw Error("APPLICATION_NOT_FOUND", "Owner application was not found.", 404);
        var user = await db.LockUserAsync(application.UserId, ct);
        await db.LockOwnerAccountsAsync(application.UserId, ct);
        if (body.Status == "approved" && application.Status == "approved" && user.DeletedOn is null)
        {
            application.User = user;
            var deliveries = await db.ReadOwnerDecisionDeliveriesAsync(ct);
            var delivery = deliveries.SingleOrDefault(d => d.UserId == user.Id);
            await tx.CommitAsync(ct);
            return Result(application) with { ContactVerified = user.EmailVerifiedAt is not null,
                DeliveryId = delivery?.Id, DeliveryStatus = delivery?.Status };
        }
        if (intent is not null && (intent.BusinessName != application.BusinessName || intent.Email != user.Email ||
            intent.Phone != user.Phone || intent.LotJson != application.LotSetupJson))
            throw Error("APPROVAL_CONFLICT", "Approval context changed; reconcile the existing provisioning operation.", 409);
        if (application.Status != "pending" || user.DeletedOn != null || user.Status != UserStatus.PendingApproval)
            throw Error("APPLICATION_CLOSED", "Owner application is no longer pending.", 409);
        if (application.ApprovalRequestedBy is not null && body.Status != "approved")
            throw Error("APPROVAL_IN_PROGRESS", "Approval has started. Retry approval to finish provisioning.", 409);
        if (body.Status == "approved" && application.LotSetupJson is null)
            throw Error("LOT_SETUP_REQUIRED", "Complete parking lot setup before approval.", 409);
        if (user.EmailVerifiedAt is null)
            throw Error("CONTACT_UNVERIFIED", "Verify required contact before approval.", 409);
        var account = user.Accounts.SingleOrDefault(a => a.DeletedOn == null && a.Status == "PENDING_APPROVAL" &&
            a.AccountRoles.Any(r => r.RoleCode == "BUSINESS_OWNER"));
        if (account is null) throw Error("APPLICATION_CLOSED", "Owner account is no longer eligible for review.", 409);
        if (body.Status == "approved")
            await db.EnsureOwnerTenantAsync(application.Id, application.BusinessName, user.Email!, user.Phone!, ct);
        application.User = user;
        application.Status = body.Status;
        application.ReviewedBy = application.ApprovalRequestedBy ?? adminId;
        application.ReviewedAt = clock.GetUtcNow();
        if (application.ApprovalRequestedBy is null) application.ReviewNote = body.ReviewNote?.Trim();
        user.Status = body.Status == "approved" ? UserStatus.Active : UserStatus.Rejected;
        user.ModifiedOn = clock.GetUtcNow();
        if (body.Status == "approved")
        {
            // Deterministic ID makes remote provisioning retryable if the local commit fails.
            var tenantId = application.Id;
            user.OnboardingComplete = true;
            account.TenantId = tenantId;
            account.Status = "ACTIVE";
        }
        else account.Status = "INACTIVE";
        await db.SaveChangesAsync(ct);
        var deliveryId=await workflows.QueueAsync(user.Id!.Value,adminId,"OWNER_DECISION",new("email",user.Email!,"SmartPark application decision",$"Your application is {application.Status}."),ct);
        await tx.CommitAsync(ct);
        return Result(application) with { ContactVerified = true,DeliveryId=deliveryId,DeliveryStatus="pending" };
    }
    private sealed record ApprovalIntent(string BusinessName, string Email, string Phone, string LotJson, bool Completed);

    private async Task<ApprovalIntent> RecordApprovalIntentAsync(Guid adminId, Guid id, ReviewOwnerDto body, CancellationToken ct)
    {
        await using var tx = await db.BeginTransactionAsync(ct);
        await RequireAdmin(adminId, ct);
        var application = await db.LockOwnerApplicationAsync(id, ct)
            ?? throw Error("APPLICATION_NOT_FOUND", "Owner application was not found.", 404);
        var user = await db.LockUserAsync(application.UserId, ct);
        if (application.Status == "approved" && user.DeletedOn is null)
        {
            await tx.CommitAsync(ct);
            return new(application.BusinessName, user.Email!, user.Phone!, application.LotSetupJson!, true);
        }
        if (application.Status != "pending" || user.DeletedOn is not null || user.Status != UserStatus.PendingApproval)
            throw Error("APPLICATION_CLOSED", "Owner application is no longer pending.", 409);
        if (user.EmailVerifiedAt is null) throw Error("CONTACT_UNVERIFIED", "Verify email before approval.", 409);
        if (application.LotSetupJson is null) throw Error("LOT_SETUP_REQUIRED", "Complete parking lot setup before approval.", 409);
        if (!await db.HasPendingOwnerAccountAsync(application.UserId, ct))
            throw Error("APPLICATION_CLOSED", "Owner account is no longer eligible for review.", 409);
        if (application.ApprovalRequestedBy is null)
        {
            application.ApprovalRequestedBy = adminId;
            application.ReviewNote = body.ReviewNote?.Trim();
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
        return new(application.BusinessName, user.Email!, user.Phone!, application.LotSetupJson!, false);
    }
}
