namespace SmartParking.PaymentService.Infrastructure.Adapters.Outbound.Models;

using System.Text.Json;
using SmartParking.PaymentService.Domain.Pricing;

public sealed record ReservationTimeBlockRuleHttpDto(
    string BlockCode,
    TimeOnly StartTime,
    TimeOnly EndTime,
    long RatePerHourVnd
);

public sealed class ReservationPricingSnapshotHttpDto
{
    public Guid TariffId { get; set; }
    public string TariffName { get; set; } = string.Empty;
    public int PolicyVersion { get; set; }
    public JsonElement VehicleType { get; set; }
    public int FreeParkingGraceMinutes { get; set; }
    public List<ReservationTimeBlockRuleHttpDto>? TimeBlocks { get; set; }
    public string? TimeZoneId { get; set; }
    public JsonElement RoundingMode { get; set; }

    public PricingPolicySnapshot ToDomain()
    {
        var vehicleType = ParseVehicleType(VehicleType);
        var roundingMode = ParseRoundingMode(RoundingMode);

        if (TimeBlocks == null || TimeBlocks.Count == 0)
        {
            throw new PricingDomainException(
                "CORRUPTED_POLICY_SNAPSHOT",
                "Snapshot must contain at least one time block.",
                "reservationId");
        }

        var domainBlocks = TimeBlocks.Select(b => new TimeBlockRule(
            b.BlockCode ?? "BLOCK",
            b.StartTime,
            b.EndTime,
            b.RatePerHourVnd
        )).ToList();

        return new PricingPolicySnapshot(
            TariffId: TariffId,
            TariffName: string.IsNullOrWhiteSpace(TariffName) ? "Reservation Tariff" : TariffName,
            PolicyVersion: PolicyVersion > 0 ? PolicyVersion : 1,
            VehicleType: vehicleType,
            FreeParkingGraceMinutes: FreeParkingGraceMinutes >= 0 ? FreeParkingGraceMinutes : 0,
            TimeBlocks: domainBlocks,
            TimeZoneId: string.IsNullOrWhiteSpace(TimeZoneId) ? "Asia/Ho_Chi_Minh" : TimeZoneId,
            RoundingMode: roundingMode
        );
    }

    private static PricingVehicleType ParseVehicleType(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            if (Enum.TryParse<PricingVehicleType>(element.GetString(), ignoreCase: true, out var vt))
            {
                return vt;
            }
        }
        else if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var vtInt) && Enum.IsDefined(typeof(PricingVehicleType), vtInt))
        {
            return (PricingVehicleType)vtInt;
        }

        throw new PricingDomainException(
            "CORRUPTED_POLICY_SNAPSHOT",
            $"Invalid vehicle type in reservation snapshot: '{element}'.",
            "reservationId");
    }

    private static MonetaryRoundingMode ParseRoundingMode(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Undefined || element.ValueKind == JsonValueKind.Null)
        {
            return MonetaryRoundingMode.HALF_UP_THOUSAND;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            if (Enum.TryParse<MonetaryRoundingMode>(element.GetString(), ignoreCase: true, out var rm))
            {
                return rm;
            }
        }
        else if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var rmInt) && Enum.IsDefined(typeof(MonetaryRoundingMode), rmInt))
        {
            return (MonetaryRoundingMode)rmInt;
        }

        throw new PricingDomainException(
            "CORRUPTED_POLICY_SNAPSHOT",
            $"Invalid rounding mode in reservation snapshot: '{element}'.",
            "reservationId");
    }
}
