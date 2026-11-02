using System.Security.Cryptography;
using System.Text.Json;
using SmartParking.UserService.Domain.Entities;
using UserService.Application.Common.Interfaces.Services;

namespace UserService.Application.Services;

public sealed class OperatorInvitationFactory(IWorkflowProtector protector) : IOperatorInvitationFactory
{
    public OperatorInvitation Create(Guid userId, Guid actorId, string email, DateTimeOffset now)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var challenge = new AuthChallenge
        {
            Id = Guid.NewGuid(), UserId = userId, Purpose = "BOOTSTRAP_PASSWORD",
            Channel = "email", Destination = email, CodeHash = AuthSessionService.HashRefreshToken(token),
            ExpiresAt = now.AddHours(24), ResendAt = now.AddMinutes(1)
        };
        var message = new WorkflowMessage("email", email, "Verify your SmartPark Operator account",
            $"Verify your email and choose your own password to activate your account (24 hours): /change-password?challengeId={challenge.Id}&token={token}",
            EmailTemplate: WorkflowEmailTemplates.OperatorInvitation);
        var delivery = new WorkflowDelivery
        {
            Id = Guid.NewGuid(), UserId = userId, ActorId = actorId,
            Kind = "OPERATOR_ONBOARDING", ChallengeId = challenge.Id, CreatedAt = now,
            ExpiresAt = challenge.ExpiresAt, ProtectedPayload = protector.Protect(JsonSerializer.Serialize(message))
        };
        return new(challenge, delivery);
    }
}
