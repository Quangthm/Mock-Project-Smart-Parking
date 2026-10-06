namespace SmartParking.UserService.Domain.Entities;

public sealed class OwnerApplication
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string BusinessName { get; set; } = "";
    public string LotType { get; set; } = "";
    public string Status { get; set; } = "pending";
    public DateTimeOffset SubmittedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public Guid? ReviewedBy { get; set; }
    public string? ReviewNote { get; set; }
}
