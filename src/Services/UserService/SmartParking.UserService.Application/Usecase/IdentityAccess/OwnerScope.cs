using FluentValidation;
using MediatR;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Models.Exceptions;

namespace UserService.Application.Usecase.IdentityAccess;

public sealed record OwnerScopeQuery(Guid Actor, string Permission) : IRequest<OwnerScope>;
public sealed record OwnerScope(Guid UserId, Guid[] TenantIds);
public sealed class OwnerScopeValidator : AbstractValidator<OwnerScopeQuery>
{
    public OwnerScopeValidator()
    {
        RuleFor(x => x.Actor).NotEmpty();
        RuleFor(x => x.Permission).Must(p => p is "PARKING_MANAGE" or "BACKUP_CONFIGURE" or "BACKUP_MARK");
    }
}
public sealed class OwnerScopeHandler(IIdentityAccessRepository repository) : IRequestHandler<OwnerScopeQuery, OwnerScope>
{
    public async Task<OwnerScope> Handle(OwnerScopeQuery request, CancellationToken cancellationToken)
    {
        var tenants = await repository.OwnerTenantsAsync(request.Actor, request.Permission, cancellationToken);
        if (tenants.Length == 0) throw new AuthException("FORBIDDEN", "Current Owner permission is required.", 403);
        return new(request.Actor, tenants);
    }
}
