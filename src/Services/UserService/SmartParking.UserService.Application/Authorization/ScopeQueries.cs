using FluentValidation;
using MediatR;
using UserService.Application.Common.Interfaces.Services;

namespace UserService.Application.Authorization;

public sealed record GetOwnerScopeQuery(string Permission) : IRequest<IdentityScope>;
public sealed class GetOwnerScopeQueryValidator : AbstractValidator<GetOwnerScopeQuery>
{
    public GetOwnerScopeQueryValidator() => RuleFor(q => q.Permission)
        .Must(p => p is "PARKING_MANAGE" or "BACKUP_CONFIGURE" or "BACKUP_MARK")
        .WithMessage("Unsupported owner-scope permission.");
}

public sealed class GetOwnerScopeQueryHandler(IAccountAuthorizationService authorization)
    : IRequestHandler<GetOwnerScopeQuery, IdentityScope>
{
    public Task<IdentityScope> Handle(GetOwnerScopeQuery request, CancellationToken ct) =>
        authorization.OwnerScopeAsync(request.Permission switch
        {
            "PARKING_MANAGE" => OwnerScopeAction.ParkingManage,
            "BACKUP_CONFIGURE" => OwnerScopeAction.BackupConfigure,
            "BACKUP_MARK" => OwnerScopeAction.BackupMark,
            _ => throw new UserService.Application.Common.Models.Exceptions.AuthException("VALIDATION_FAILED", "Unsupported owner-scope permission.", 400)
        }, ct);
}

public sealed record GetOperatorScopeQuery(Guid SiteId, OperatorScopeAction Action) : IRequest<IdentityScope>;
public sealed class GetOperatorScopeQueryHandler(IAccountAuthorizationService authorization)
    : IRequestHandler<GetOperatorScopeQuery, IdentityScope>
{
    public Task<IdentityScope> Handle(GetOperatorScopeQuery request, CancellationToken ct) =>
        authorization.OperatorScopeAsync(request.SiteId, request.Action, ct);
}
