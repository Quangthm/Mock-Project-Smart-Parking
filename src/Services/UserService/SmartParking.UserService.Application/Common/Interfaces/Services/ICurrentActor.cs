namespace UserService.Application.Common.Interfaces.Services;

/// <summary>
/// The trusted identity of the caller behind the current request.
/// </summary>
/// <remarks>
/// The port is inward-facing: the Application layer asks for the caller and never reads the
/// transport itself. The role claim is informational only; authorization always re-checks
/// the current account state against the authoritative record.
/// </remarks>
public interface ICurrentActor
{
    /// <summary>The caller's user id, or null when the request carries no authenticated subject.</summary>
    Guid? UserId { get; }

    /// <summary>The exact account selected when the authenticated session was issued.</summary>
    Guid? AccountId => null;

    /// <summary>The authenticated access token's jti, rechecked against persisted session state.</summary>
    Guid? AccessTokenId => null;

    /// <summary>The caller's role claim, or null when the request carries no authenticated subject.</summary>
    string? Role { get; }
    string CorrelationId => string.Empty;
}
