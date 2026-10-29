using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.DTOs;

namespace UserService.Persistence.Repositories;

public sealed class OperatorProvisioningService(AppDbContext db, IPasswordService passwords, TimeProvider clock, AccountWorkflowService workflows,
    IAccountAuthorizationService authorization)
    : IOperatorProvisioningService
{
    private static AuthException Error(string code, string message, int status = 400) => new(code, message, status);

    public async Task<IReadOnlyList<OperatorDto>> ListAsync(Guid ownerId, CancellationToken ct)
    {
        var tenants = (await authorization.RequireOwnerManagementAsync(ownerId, false, ct)).TenantIds;
        var grants = await db.OperatorGrants.AsNoTracking().Include(g => g.Account).ThenInclude(a => a.User)
            .Where(g => g.CreatedBy == ownerId && g.Account.DeletedOn == null && g.Account.User.DeletedOn == null &&
                g.Account.TenantId != null && tenants.Contains(g.Account.TenantId.Value)).ToListAsync(ct);
        var deliveries=await db.WorkflowDeliveries.AsNoTracking().Where(d=>d.ActorId==ownerId && d.Kind=="OPERATOR_ONBOARDING").ToArrayAsync(ct);
        return grants.GroupBy(g => g.Account.UserId).Select(group =>
        {
            var user = group.First().Account.User;
            var delivery=deliveries.SingleOrDefault(d=>d.UserId==user.Id);
            return new OperatorDto(user.Id!.Value, user.FullName, user.Email!, "operator", user.Status.ToString().ToLowerInvariant(),
                ownerId, group.Select(g => g.Account.SiteId!.Value).ToArray(), group.SelectMany(g => g.Permissions).Distinct().ToArray(),delivery?.Id,delivery?.Status??"pending");
        }).OrderBy(o => o.FullName).ToArray();
    }

    public async Task<OperatorDto> CreateAsync(Guid ownerId, CreateOperatorDto body, CancellationToken ct)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(body, new ValidationContext(body), errors, true))
            throw Error("VALIDATION_FAILED", string.Join(" ", errors.Select(e => e.ErrorMessage)));
        var email = body.Email.Trim().ToLowerInvariant();
        var siteIds = body.SiteIds.Order().ToArray();
        var permissions = body.Permissions.Order(StringComparer.Ordinal).ToArray();
        var hash = passwords.Hash(body.Password);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var sites = await authorization.RequireManagedSitesAsync(ownerId, siteIds, ct);
        if (await db.Users.AnyAsync(u => u.DeletedOn == null && u.Email != null && u.Email.ToLower() == email, ct))
            throw Error("EMAIL_EXISTS", "Email is already registered.", 409);
        var now = clock.GetUtcNow();
        var user = new User { Id = Guid.NewGuid(), FullName = body.FullName.Trim(), Email = email,
            PasswordHash = hash, Status = UserStatus.Active, CreatedOn = now, ModifiedOn = now };
        foreach (var site in sites)
        {
            var account = new Account { Id = Guid.NewGuid(), User = user, UserId = user.Id.Value,
                TenantId = site.TenantId, SiteId = site.Id,
                Status = "ACTIVE", CreatedOn = now, RoleCode = "SITE_OPERATOR" };
            user.Accounts.Add(account);
            db.OperatorGrants.Add(new OperatorGrant { AccountId = account.Id.Value, Account = account,
                CreatedBy = ownerId, CreatedAt = now, Permissions = permissions.ToArray() });
        }
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23505" })
        { throw Error("EMAIL_EXISTS", "Email is already registered.", 409); }
        var deliveryId=await workflows.QueueOnboardingAsync(user,ownerId,body.Password,ct);
        await tx.CommitAsync(ct);
        return new(user.Id.Value, user.FullName, email, "operator", "active", ownerId, siteIds, permissions,deliveryId);
    }

}
