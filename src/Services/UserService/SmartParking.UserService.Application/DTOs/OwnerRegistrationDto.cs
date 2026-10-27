using System.ComponentModel.DataAnnotations;
using UserService.Application.Common.Validation;

namespace UserService.Application.DTOs;

[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed class OwnerRegistrationDto
{
    [Required, StringLength(255)] public string FullName { get; set; } = "";
    [Required, StringLength(255)] public string BusinessName { get; set; } = "";
    [Required, AccountEmail, StringLength(255)] public string Email { get; set; } = "";
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

public sealed class OwnerDeviceDto
{
    [Required] public string Type { get; set; } = "";
    public string? Label { get; set; }
    public bool Enabled { get; set; } = true;
}

public sealed class OwnerLotSetupDto
{
    [Required, StringLength(255)] public string Name { get; set; } = "";
    [Required, StringLength(500)] public string Address { get; set; } = "";
    [Required, RegularExpression("^(outdoor|basement|multi-storey)$")] public string LotType { get; set; } = "";
    [Range(1, 2000)] public int TotalSlots { get; set; }
    [Range(1, 20)] public int Floors { get; set; } = 1;
    public List<OwnerDeviceDto>? Devices { get; set; } = [];
    public string? ModelFileName { get; set; }
    [Range(0, 100_000_000)] public decimal HourlyRate { get; set; }
    [Range(0, 100_000_000)] public decimal DailyRate { get; set; }
    [Range(0, 100_000_000)] public decimal NightRate { get; set; }
    [Range(0, 1440)] public int GracePeriodMinutes { get; set; }
    [Range(0, 1_000_000_000)] public decimal SubscriptionMonthly { get; set; }
    [Range(0, 10_000_000_000)] public decimal SubscriptionYearly { get; set; }
    [Range(typeof(bool), "true", "true", ErrorMessage = "Parking policy must be accepted.")]
    public bool PolicyAccepted { get; set; }
}

public sealed class SubmitLotSetupRequestDto
{
    [Required] public Guid RegistrationId { get; set; }
    [Required] public string SetupToken { get; set; } = "";
    [Required] public OwnerLotSetupDto Lot { get; set; } = null!;
}

public sealed record OwnerApplicationDto(
    Guid Id,
    Guid OwnerId,
    string OwnerName,
    string BusinessName,
    string Email,
    string Phone,
    string LotType,
    string Status,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? ReviewedAt,
    Guid? ReviewedBy,
    string? ReviewNote,
    bool ContactVerified = false,
    ChallengeResult? Verification = null,
    Guid? DeliveryId = null,
    string? DeliveryStatus = null,
    bool LotSetupComplete = false,
    OwnerLotSetupDto? LotSetup = null,
    string? SetupToken = null);
