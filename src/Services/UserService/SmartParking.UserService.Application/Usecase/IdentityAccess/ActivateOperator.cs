using FluentValidation;
using MediatR;
using System.Security.Cryptography;
using System.Text;
using SmartParking.UserService.Domain.Entities;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Interfaces.MessageBroker;
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
            When(x => !string.IsNullOrEmpty(x.Body.Password), () =>
            {
                RuleFor(x => x.Body.Password).Length(8, 15)
                    .Matches(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*[0-9])(?=.*[^A-Za-z0-9]).+$");
            });
        });
    }
}
public sealed class ActivateOperatorHandler(IIdentityAccessRepository repository, IUnitOfWork unitOfWork,
    IPasswordService passwords, TimeProvider clock, IEventPublisher events) : IRequestHandler<ActivateOperatorCommand>
{
    public async Task Handle(ActivateOperatorCommand request, CancellationToken cancellationToken)
    {
        var passwordHash = string.IsNullOrEmpty(request.Body.Password) ? null : passwords.Hash(request.Body.Password);
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var invitation = await repository.LockInvitationAsync(request.Body.ChallengeId, cancellationToken);
        var now = clock.GetUtcNow();
        if (invitation is not { } value || !OperatorActivation.CanActivate(value.User, value.Challenge, now) ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(value.Challenge.CodeHash),
                Encoding.UTF8.GetBytes(AuthSessionService.HashRefreshToken(request.Body.Token))))
            throw new AuthException("INVALID_TOKEN", "Activation link is invalid or expired.", 401);

        var simulated = await repository.FindSimulatedIdentityAsync(value.User.Id!.Value, cancellationToken);
        if (simulated is null)
        {
            var sim = new UserSimulatedIdentity
            {
                Id = Guid.NewGuid(),
                UserId = value.User.Id.Value,
                User = value.User,
                SimulatedVNeIdGuid = Guid.NewGuid(),
                ConsentGiven = true,
                ConsentTimestamp = now,
                ConsentVersion = "v1.1-simulated",
                SimulationStatus = "COMPLETED",
                MockDocumentNumber = $"001090{RandomNumberGenerator.GetInt32(100000, 999999):D6}",
                MockFullName = value.User.FullName.ToUpperInvariant(),
                CreatedOn = now,
                ModifiedOn = now
            };
            repository.AddSimulatedIdentity(sim);
        }

        OperatorActivation.Activate(value.User, value.Challenge, passwordHash, now);
        await repository.RevokeSessionsAsync(value.User.Id!.Value, cancellationToken);
        await events.PublishAsync(new OperatorAccessChanged(Guid.NewGuid(), 1, value.User.Id.Value, value.User.AccessVersion, now,
            value.Challenge.Id, value.User.Id.Value, value.User.Accounts.Where(a => a.TenantId != null && a.DeletedOn == null)
                .Select(a => a.TenantId!.Value).Distinct().ToArray(), "active"), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
