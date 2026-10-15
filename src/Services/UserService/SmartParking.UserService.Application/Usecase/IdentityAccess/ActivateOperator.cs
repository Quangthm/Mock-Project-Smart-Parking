using FluentValidation;
using MediatR;
using System.Security.Cryptography;
using System.Text;
using SmartParking.UserService.Domain.Entities;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.DTOs;
using UserService.Application.Services;

namespace UserService.Application.Usecase.IdentityAccess;

public sealed record ActivateOperatorCommand(ChangeBootstrapPasswordDto Body) : IRequest;
public sealed class ActivateOperatorValidator : AbstractValidator<ActivateOperatorCommand>
{
    public ActivateOperatorValidator()
    {
        RuleFor(x => x.Body).NotNull();
        When(x => x.Body is not null, () =>
        {
            RuleFor(x => x.Body.ChallengeId).NotEmpty();
            RuleFor(x => x.Body.Token).NotEmpty().Length(64).Matches("^[0-9A-Fa-f]{64}$");
            RuleFor(x => x.Body.Password).NotEmpty().Length(8, 15)
                .Matches(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*[0-9])(?=.*[^A-Za-z0-9]).+$");
        });
    }
}
public sealed class ActivateOperatorHandler(IIdentityAccessRepository repository, IUnitOfWork unitOfWork,
    IPasswordService passwords, TimeProvider clock, IAccessEventStore events) : IRequestHandler<ActivateOperatorCommand>
{
    public async Task Handle(ActivateOperatorCommand request, CancellationToken cancellationToken)
    {
        var passwordHash = passwords.Hash(request.Body.Password);
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var invitation = await repository.LockInvitationAsync(request.Body.ChallengeId, cancellationToken);
        var now = clock.GetUtcNow();
        if (invitation is not { } value || !OperatorActivation.CanActivate(value.User, value.Challenge, now) ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(value.Challenge.CodeHash),
                Encoding.UTF8.GetBytes(AuthSessionService.HashRefreshToken(request.Body.Token))))
            throw new AuthException("INVALID_TOKEN", "Activation link is invalid or expired.", 401);
        OperatorActivation.Activate(value.User, value.Challenge, passwordHash, now);
        await repository.RevokeSessionsAsync(value.User.Id!.Value, cancellationToken);
        events.Stage(new(Guid.NewGuid(), 1, value.User.Id.Value, value.User.AccessVersion, now,
            value.Challenge.Id, value.User.Id.Value, value.User.Accounts.Where(a => a.TenantId != null && a.DeletedOn == null)
                .Select(a => a.TenantId!.Value).Distinct().ToArray(), "active"));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
