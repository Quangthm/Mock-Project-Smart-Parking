namespace SmartParking.IoTService.Application.Assistant.Authorization;

// Identity asserted by a locally validated access token, not live Account/session state.
public sealed record CurrentActor(Guid AccountId, Guid UserId, Guid AccessTokenId, string Role, DateTimeOffset ExpiresAt);

public interface ICurrentActor
{
    CurrentActor? Actor { get; }
}
