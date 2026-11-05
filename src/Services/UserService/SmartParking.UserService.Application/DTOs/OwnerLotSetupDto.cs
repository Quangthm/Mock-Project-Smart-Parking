using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace UserService.Application.DTOs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class OwnerLotSetupRequest
{
    public Guid RegistrationId { get; set; }
    [Required, StringLength(128)] public string SetupToken { get; set; } = "";
    [Required] public OwnerLotSetupDto Lot { get; set; } = null!;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class OwnerLotSetupDto : IValidatableObject
{
    [Required, StringLength(255)] public string Name { get; set; } = "";
    [Required, StringLength(2000)] public string Address { get; set; } = "";
    [Required, RegularExpression("^(outdoor|basement|multi-storey)$")] public string LotType { get; set; } = "";
    [Range(1, 2000)] public int TotalSlots { get; set; }
    [Range(1, 20)] public int Floors { get; set; }
    [Required, MaxLength(5)] public OwnerDeviceDto[] Devices { get; set; } = [];
    [Required] public string ModelFileName { get; set; } = "";
    [Range(0, 9007199254740991L)] public long HourlyRate { get; set; }
    [Range(0, 9007199254740991L)] public long DailyRate { get; set; }
    [Range(0, 9007199254740991L)] public long NightRate { get; set; }
    [Range(0, int.MaxValue)] public int GracePeriodMinutes { get; set; }
    [Range(0, 9007199254740991L)] public long SubscriptionMonthly { get; set; }
    [Range(0, 9007199254740991L)] public long SubscriptionYearly { get; set; }
    [Range(typeof(bool), "true", "true")] public bool PolicyAccepted { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        var expected = LotType switch { "outdoor" => "outdoor_parking_lot.glb", "basement" => "underground_parking_lot.glb", _ => "indoor_parking_lot.glb" };
        if (ModelFileName != expected && !(LotType == "outdoor" && ModelFileName == "outdoor_parking_lot_.glb"))
            yield return new("Select a supported model matching the lot type.");
        if (LotType == "outdoor" && Floors != 1) yield return new("Outdoor lots must have one floor.");
        if (Devices is null) yield break;
        if (Devices.Any(d => d is null || d.Type is not ("camera" or "rfid" or "ev-charger" or "sensor" or "barrier") || string.IsNullOrWhiteSpace(d.Label) || d.Label.Length > 100))
            yield return new("Invalid device configuration.");
        if (Devices.Where(d => d is not null).Select(d => d.Type).Distinct().Count() != Devices.Length)
            yield return new("Device types must be unique.");
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OwnerDeviceDto(string Type, string Label, bool Enabled);
