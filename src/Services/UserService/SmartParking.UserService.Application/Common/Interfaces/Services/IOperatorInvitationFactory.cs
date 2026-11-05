using SmartParking.UserService.Domain.Entities;

namespace UserService.Application.Common.Interfaces.Services;

public sealed record OperatorInvitation(AuthChallenge Challenge, WorkflowDelivery Delivery);

public interface IOperatorInvitationFactory
{
    OperatorInvitation Create(Guid userId, Guid actorId, string email, DateTimeOffset now);
}
