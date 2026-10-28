using System.Text.Json;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Pricing;

namespace SmartParking.PaymentService.Persistence.Adapters;

public static class TariffSnapshotParser
{
    public static PricingPolicySnapshot Parse(Tariff tariff, int policyVersion = 1)
    {
        ArgumentNullException.ThrowIfNull(tariff, nameof(tariff));

        var vehicleType = Enum.TryParse<PricingVehicleType>(tariff.VehicleType, true, out var vt)
            ? vt
            : PricingVehicleType.CAR;

        var timeBlocks = new List<TimeBlockRule>();
        string timeZoneId = "Asia/Ho_Chi_Minh";
        var roundingMode = MonetaryRoundingMode.HALF_UP_THOUSAND;

        if (!string.IsNullOrWhiteSpace(tariff.TariffRulesJson) && tariff.TariffRulesJson.Trim() != "{}")
        {
            try
            {
                using var doc = JsonDocument.Parse(tariff.TariffRulesJson);
                var root = doc.RootElement;

                if (root.TryGetProperty("timeZoneId", out var tzProp) || root.TryGetProperty("time_zone_id", out tzProp))
                {
                    var tz = tzProp.GetString();
                    if (!string.IsNullOrWhiteSpace(tz)) timeZoneId = tz;
                }

                if (root.TryGetProperty("roundingMode", out var rmProp) || root.TryGetProperty("rounding_mode", out rmProp))
                {
                    if (Enum.TryParse<MonetaryRoundingMode>(rmProp.GetString(), true, out var rm))
                    {
                        roundingMode = rm;
                    }
                }

                JsonElement blocksElement = default;
                if (root.TryGetProperty("timeBlocks", out var tbProp) || root.TryGetProperty("time_blocks", out tbProp))
                {
                    blocksElement = tbProp;
                }
                else if (root.ValueKind == JsonValueKind.Array)
                {
                    blocksElement = root;
                }

                if (blocksElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in blocksElement.EnumerateArray())
                    {
                        string blockCode = item.TryGetProperty("blockCode", out var bc) ? bc.GetString() ?? "BLOCK"
                            : item.TryGetProperty("block_code", out bc) ? bc.GetString() ?? "BLOCK"
                            : "BLOCK";

                        string startStr = item.TryGetProperty("startTime", out var st) ? st.GetString() ?? "00:00"
                            : item.TryGetProperty("start_time", out st) ? st.GetString() ?? "00:00"
                            : "00:00";

                        string endStr = item.TryGetProperty("endTime", out var et) ? et.GetString() ?? "00:00"
                            : item.TryGetProperty("end_time", out et) ? et.GetString() ?? "00:00"
                            : "00:00";

                        long rate = item.TryGetProperty("ratePerHourVnd", out var r) ? r.GetInt64()
                            : item.TryGetProperty("rate_per_hour_vnd", out r) ? r.GetInt64()
                            : item.TryGetProperty("pricePerHourVnd", out r) ? r.GetInt64()
                            : 0L;

                        var startTime = TimeOnly.Parse(startStr);
                        var endTime = TimeOnly.Parse(endStr);

                        timeBlocks.Add(new TimeBlockRule(blockCode, startTime, endTime, rate));
                    }
                }
            }
            catch (JsonException)
            {
                // Fallback default 24h block if json is not parsable
            }
        }

        if (timeBlocks.Count == 0)
        {
            timeBlocks.Add(new TimeBlockRule("ALL_DAY", new TimeOnly(0, 0), new TimeOnly(0, 0), 20000L));
        }

        return new PricingPolicySnapshot(
            TariffId: tariff.Id,
            TariffName: tariff.Name,
            PolicyVersion: policyVersion,
            VehicleType: vehicleType,
            FreeParkingGraceMinutes: tariff.FreeParkingGraceMinutes,
            TimeBlocks: timeBlocks,
            TimeZoneId: timeZoneId,
            RoundingMode: roundingMode
        );
    }
}
