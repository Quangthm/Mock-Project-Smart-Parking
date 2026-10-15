using System.ComponentModel.DataAnnotations;
using UserService.Application.Common.Validation;

namespace UserService.Application.DTOs;

[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed class DriverRegistrationDto
{
    [Required, StringLength(255)] public string FullName { get; set; } = "";
    [AccountEmail, StringLength(255)] public string? Email { get; set; }
    [RegularExpression(@"^\+?[0-9]{9,15}$")] public string? Phone { get; set; }
    [StringLength(15, MinimumLength = 8)]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*[0-9])(?=.*[^A-Za-z0-9]).+$")]
    public string? Password { get; set; }
}

public sealed class VerifyDriverOtpDto
{
    public Guid RegistrationId { get; set; }
    [Required, RegularExpression(@"^[0-9]{6}$")] public string Code { get; set; } = "";
}

public sealed class ResendDriverOtpDto
{
    public Guid RegistrationId { get; set; }
}

public sealed record DriverRegistrationResult(Guid RegistrationId, string Channel,
    DateTimeOffset ExpiresAt, DateTimeOffset ResendAvailableAt);
