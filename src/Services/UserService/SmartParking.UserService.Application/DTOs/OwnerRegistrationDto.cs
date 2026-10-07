using System.ComponentModel.DataAnnotations;

namespace UserService.Application.DTOs;

[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed class OwnerRegistrationDto
{
    [Required, StringLength(255)] public string FullName { get; set; } = "";
    [Required, StringLength(255)] public string BusinessName { get; set; } = "";
    [Required, EmailAddress, StringLength(255)] public string Email { get; set; } = "";
    [Required, RegularExpression(@"^\+?[0-9]{9,15}$")] public string Phone { get; set; } = "";
    [StringLength(15, MinimumLength = 8)]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*[0-9])(?=.*[^A-Za-z0-9]).+$")]
    public string? Password { get; set; }
    [Required, RegularExpression("^(outdoor|basement|multi-storey)$")] public string LotType { get; set; } = "";
    [Range(typeof(bool), "true", "true", ErrorMessage = "Partner terms must be accepted.")]
    public bool AgreedToPolicy { get; set; }
}

[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed class ReviewOwnerDto
{
    [Required, RegularExpression("^(approved|rejected)$")] public string Status { get; set; } = "";
    [StringLength(2000)] public string? ReviewNote { get; set; }
}

public sealed record OwnerApplicationDto(Guid Id, Guid OwnerId, string OwnerName, string BusinessName,
    string Email, string Phone, string LotType, string Status, DateTimeOffset SubmittedAt,
    DateTimeOffset? ReviewedAt, Guid? ReviewedBy, string? ReviewNote, bool ContactVerified=false, ChallengeResult? Verification=null, Guid? DeliveryId=null,string? DeliveryStatus=null,ChallengeResult? PhoneVerification=null);
