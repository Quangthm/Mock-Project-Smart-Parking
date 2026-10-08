namespace SmartParking.UserService.Domain.Entities;

// Each account represents exactly one assigned parking site; never a tenant-wide wildcard.
public sealed class OperatorGrant
{
    public Guid AccountId { get; set; }
    public Account Account { get; set; } = null!;
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string[] Permissions { get; set; } = [];
}
