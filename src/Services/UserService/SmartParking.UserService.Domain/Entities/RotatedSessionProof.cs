namespace SmartParking.UserService.Domain.Entities;

// Hash-only proof retained until the logical session expires. It can revoke, never refresh or authenticate.
public sealed class RotatedSessionProof
{
    public Guid AccessTokenId { get; set; }
    public Guid SessionId { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTimeOffset AccessExpiresAt { get; set; }
}
