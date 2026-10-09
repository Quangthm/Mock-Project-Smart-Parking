using System.ComponentModel.DataAnnotations;
using UserService.Application.Common.Validation;

namespace UserService.Application.DTOs;

[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed class CreateOperatorDto : IValidatableObject
{
    [Required, StringLength(255)] public string FullName { get; set; } = "";
    [Required, AccountEmail, StringLength(255)] public string Email { get; set; } = "";
    // BCrypt considers at most 72 UTF-8 bytes. Match existing login's 8-15 character policy.
    [Required, StringLength(15, MinimumLength = 8)]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*[0-9])(?=.*[^A-Za-z0-9]).+$")]
    public string Password { get; set; } = "";
    [Required] public Guid[] SiteIds { get; set; } = [];
    [Required] public string[] Permissions { get; set; } = [];

    public static readonly IReadOnlySet<string> DelegablePermissions = new HashSet<string>(StringComparer.Ordinal)
        { "DEVICE_MANAGE", "DEVICE_STATUS_VIEW", "CASH_COLLECT", "APPEAL_REVIEW", "SLOT_OVERRIDE" };

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (SiteIds == null || SiteIds.Length is < 1 or > 100 || SiteIds.Any(id => id == Guid.Empty)
            || SiteIds.Distinct().Count() != SiteIds.Length)
            yield return new("Select 1-100 distinct parking site IDs.", [nameof(SiteIds)]);
        if (Permissions == null || Permissions.Length <1 || Permissions.Length>DelegablePermissions.Count
            || Permissions.Any(p => p == null || !DelegablePermissions.Contains(p))
            || Permissions.Distinct().Count() != Permissions.Length)
            yield return new("Select distinct supported lot-scoped permissions.", [nameof(Permissions)]);
    }
}

public sealed record OperatorDto(Guid Id, string FullName, string Email, string Role,
    string Status, Guid CreatedBy, Guid[] SiteIds, string[] Permissions, Guid? DeliveryId=null, string DeliveryStatus="pending");
