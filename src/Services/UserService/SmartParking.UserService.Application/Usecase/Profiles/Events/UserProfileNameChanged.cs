using UserService.Application.Common.Interfaces.MessageBroker;

namespace UserService.Application.Profiles.Events;

/// <summary>
/// User & Access fact for Notification & Audit. Names and contact/security values are deliberately absent.
/// EventId is assigned once by the use case and survives relay retries and duplicate delivery.
/// </summary>
public sealed record UserProfileNameChanged(
    Guid EventId,
    Guid ActorAccountId,
    Guid AffectedUserId,
    DateTimeOffset OccurredAtUtc,
    string CorrelationId) : IIntegrationEventMessage
{
    public int SchemaVersion => 1;
}
