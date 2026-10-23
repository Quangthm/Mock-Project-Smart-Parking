using FluentValidation;
using MediatR;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.Common.Validation;
using UserService.Application.DTOs;

namespace UserService.Application.Usecase.IdentityAccess;

public sealed record CreateOperatorCommand(Guid Actor, CreateOperatorDto Body) : IRequest<OperatorDto>;
public sealed class CreateOperatorValidator : AbstractValidator<CreateOperatorCommand>
{
    public CreateOperatorValidator()
    {
        RuleFor(x => x.Actor).NotEmpty();
        RuleFor(x => x.Body).NotNull();
        When(x => x.Body is not null, () =>
        {
            RuleFor(x => x.Body.FullName).NotEmpty().MaximumLength(255);
            RuleFor(x => x.Body.Email).NotEmpty().MaximumLength(255).Must(SignInContact.IsValidEmail);
            RuleFor(x => x.Body.SiteIds).Must(ids => ids is { Length: > 0 and <= 100 } &&
                ids.All(id => id != Guid.Empty) && ids.Distinct().Count() == ids.Length);
            RuleFor(x => x.Body.Permissions).Must(p => p is { Length: > 0 and <= 5 } &&
                p.All(v => v is not null && CreateOperatorDto.DelegablePermissions.Contains(v)) && p.Distinct().Count() == p.Length);
        });
    }
}
public sealed class CreateOperatorHandler(IIdentityAccessRepository repository, IUnitOfWork unitOfWork,
    IOperatorInvitationFactory invitationFactory, TimeProvider clock, IParkingDirectory parking, IAccessEventStore events)
    : IRequestHandler<CreateOperatorCommand, OperatorDto>
{
    public async Task<OperatorDto> Handle(CreateOperatorCommand request, CancellationToken cancellationToken)
    {
        var ownerId = request.Actor;
        var body = request.Body;
        var siteIds = body.SiteIds.Order().ToArray();
        var permissions = body.Permissions.Order(StringComparer.Ordinal).ToArray();
        var email = body.Email.Trim().ToLowerInvariant();
        // Remote validation is outside the local transaction. Recheck current identity grants under lock afterwards.
        var candidateTenantIds = await RequireAuthorizedTenantIdsAsync(ownerId, cancellationToken);
        var sites = await parking.ActiveSitesAsync(siteIds, candidateTenantIds, cancellationToken);
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var tenantIds = await RequireAuthorizedTenantIdsAsync(ownerId, cancellationToken);
        if (sites.Select(site => site.Id).Distinct().Count() != siteIds.Length ||
            sites.Any(site => !siteIds.Contains(site.Id) || !tenantIds.Contains(site.TenantId)))
            throw new AuthException("SITE_ACCESS_DENIED", "Every assigned site must be active and authorized for this Owner.", 403);
        if (await repository.EmailExistsAsync(email, cancellationToken))
            throw new AuthException("EMAIL_EXISTS", "Email is already registered.", 409);
        var now = clock.GetUtcNow();
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = body.FullName.Trim(),
            Email = email,
            PasswordHash = string.Empty,
            Status = UserStatus.PendingVerification,
            CreatedOn = now,
            ModifiedOn = now,
            AccessVersion = 1
        };
        var grants = new List<OperatorGrant>();
        foreach (var site in sites)
        {
            var account = new Account
            {
                Id = Guid.NewGuid(),
                User = user,
                UserId = user.Id.Value,
                TenantId = site.TenantId,
                SiteId = site.Id,
                Status = "PENDING_VERIFICATION",
                CreatedOn = now
            };
            account.AccountRoles.Add(new AccountRole
            {
                Account = account,
                AccountId = account.Id.Value,
                RoleCode = "SITE_OPERATOR"
            });
            user.Accounts.Add(account);
            grants.Add(new OperatorGrant
            {
                AccountId = account.Id.Value,
                Account = account,
                CreatedBy = ownerId,
                CreatedAt = now,
                Permissions = permissions.ToArray()
            });
        }
        var invitation = invitationFactory.Create(user.Id.Value, ownerId, email, now);
        var challenge = invitation.Challenge;
        var delivery = invitation.Delivery;
        repository.AddOperator(user, grants, challenge, delivery);
        events.Stage(new(Guid.NewGuid(), 1, user.Id.Value, user.AccessVersion, now, challenge.Id, ownerId,
            sites.Select(site => site.TenantId).Distinct().ToArray(), "pending_verification"));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(user.Id.Value, user.FullName, email, "operator", "pending_verification", ownerId, siteIds, permissions, delivery.Id);
    }

    private async Task<Guid[]> RequireAuthorizedTenantIdsAsync(Guid actor, CancellationToken cancellationToken)
    {
        var tenantIds = await repository.OwnerTenantsAsync(actor, "OPERATOR_MANAGE", cancellationToken);
        if (tenantIds.Length == 0) throw new AuthException("FORBIDDEN", "Current Owner permission is required.", 403);
        return tenantIds;
    }

}
