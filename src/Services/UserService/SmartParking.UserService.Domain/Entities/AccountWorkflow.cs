namespace SmartParking.UserService.Domain.Entities;

public sealed class AuthChallenge
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Purpose { get; set; } = "";
    public string Channel { get; set; } = "email";
    public string Destination { get; set; } = "";
    public string CodeHash { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset ResendAt { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
}
public sealed class WorkflowDelivery
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ActorId { get; set; }
    public string Kind { get; set; } = "";
    public string ProtectedPayload { get; set; } = "";
    public string Status { get; set; } = "pending";
    public int Attempts { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? RetryAt { get; set; }
    public Guid? ChallengeId { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}
public sealed class MfaCredential
{
    public Guid UserId { get; set; }
    public string ProtectedSecret { get; set; } = "";
    public bool Enabled { get; set; }
    public long LastStep { get; set; } = -1;
    public int Attempts { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
}
public sealed class RegisteredVehicle
{
    public Guid Id { get; set; }
    public Guid? AccountId { get; set; }
    public string RawPlate { get; set; } = "";
    public string CanonicalPlate { get; set; } = "";
    public string VehicleType { get; set; } = "";
    public string? ImageReference { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
