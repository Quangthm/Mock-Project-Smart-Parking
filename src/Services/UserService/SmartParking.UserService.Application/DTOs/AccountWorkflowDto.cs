using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace UserService.Application.DTOs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class OtpLoginDto
{
    [Required, StringLength(255)] public string Contact { get; set; } = "";
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CompleteOtpLoginDto
{
    public Guid ChallengeId { get; set; }
    [Required, RegularExpression("^[0-9]{6}$")] public string Code { get; set; } = "";
    [RegularExpression("^[0-9]{6}$")] public string? Totp { get; set; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ChangeBootstrapPasswordDto
{
    public Guid ChallengeId { get; set; }
    [Required, StringLength(128)] public string Token { get; set; } = "";
    [Required, StringLength(15, MinimumLength=8), RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*[0-9])(?=.*[^A-Za-z0-9]).+$")]
    public string Password { get; set; } = "";
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class VehicleDto
{
    [Required, StringLength(30)] public string Plate { get; set; } = "";
    [Required, RegularExpression("^(CAR|MOTORCYCLE)$")] public string VehicleType { get; set; } = "";
    [Required, StringLength(2048)] public string? ImageReference { get; set; }
}
public sealed record ChallengeResult(Guid ChallengeId, DateTimeOffset ExpiresAt, DateTimeOffset ResendAt, string DeliveryStatus="pending");
