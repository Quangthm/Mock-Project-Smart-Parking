using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace UserService.Application.DTOs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class MfaVerificationDto : IValidatableObject
{
    [Required, StringLength(128)]
    public string Password { get; set; } = "";

    [RegularExpression("^[0-9]{6}$")]
    public string? Totp { get; set; }

    [StringLength(40)]
    public string? RecoveryCode { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (!string.IsNullOrWhiteSpace(Totp) && !string.IsNullOrWhiteSpace(RecoveryCode))
            yield return new("Use either an authenticator code or a recovery code.");
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CompleteMfaLoginDto : IValidatableObject
{
    public Guid ChallengeId { get; set; }

    [Required, StringLength(64, MinimumLength = 64)]
    public string ChallengeToken { get; set; } = "";

    [RegularExpression("^[0-9]{6}$")]
    public string? Totp { get; set; }

    [StringLength(40)]
    public string? RecoveryCode { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(Totp) == string.IsNullOrWhiteSpace(RecoveryCode))
            yield return new("Enter either an authenticator code or a recovery code.");
    }
}

public sealed record MfaLoginChallengeDto(Guid ChallengeId, string ChallengeToken, DateTimeOffset ExpiresAt);
public sealed record MfaStatusDto(bool Enabled, int RecoveryCodesRemaining);
public sealed record MfaSetupDto(string Secret, string Uri, DateTimeOffset ExpiresAt);
public sealed record MfaRecoveryCodesDto(string[] RecoveryCodes, bool SignInRequired);
