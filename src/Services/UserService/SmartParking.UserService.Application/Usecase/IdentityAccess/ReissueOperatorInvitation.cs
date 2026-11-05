using FluentValidation;
using MediatR;
using SmartParking.UserService.Domain.Entities;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.DTOs;

namespace UserService.Application.Usecase.IdentityAccess;

public sealed record ReissueOperatorInvitationCommand(Guid Actor, Guid OperatorId) : IRequest<ChallengeResult>;
public sealed class ReissueOperatorInvitationValidator : AbstractValidator<ReissueOperatorInvitationCommand>
{
    public ReissueOperatorInvitationValidator()
    {
        RuleFor(request => request.Actor).NotEmpty();
        RuleFor(request => request.OperatorId).NotEmpty();
    }
}
public sealed class ReissueOperatorInvitationHandler(IIdentityAccessRepository repository, IUnitOfWork unitOfWork,
    IOperatorInvitationFactory invitationFactory, TimeProvider clock)
    : IRequestHandler<ReissueOperatorInvitationCommand, ChallengeResult>
{
    public async Task<ChallengeResult> Handle(ReissueOperatorInvitationCommand request, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var tenantIds = await repository.OwnerTenantsAsync(request.Actor, "OPERATOR_MANAGE", cancellationToken);
        var user = await repository.GetOperatorForUpdateAsync(request.OperatorId, cancellationToken);
        var grants = await repository.GetOperatorGrantsAsync(request.OperatorId, cancellationToken);
        if (user is null || !OperatorActivation.CanReissueInvitation(user, grants, request.Actor, tenantIds))
            throw new AuthException("OPERATOR_NOT_FOUND", "Pending Operator was not found in your authorized scope.", 404);
        var now = clock.GetUtcNow();
        var pendingInvitations = await repository.GetPendingInvitationsAsync(request.OperatorId, cancellationToken);
        if (pendingInvitations.Any(challenge => challenge.ResendAt > now))
            throw new AuthException("OTP_COOLDOWN", "Wait before sending another invitation.", 429);
        OperatorActivation.PrepareInvitationReissue(user, grants, request.Actor, tenantIds);
        var invitation = invitationFactory.Create(request.OperatorId, request.Actor, user.Email!, now);
        await repository.ReplaceInvitationAsync(request.OperatorId, invitation.Challenge, invitation.Delivery, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(invitation.Challenge.Id, invitation.Challenge.ExpiresAt, invitation.Challenge.ResendAt);
    }
}
