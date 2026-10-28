using System.Text.Json;

namespace SmartParking.ParkingService.Application;

// Single redaction choke point for the operational-layout projection.
public static class OperationalLayoutRedaction
{
    private static readonly HashSet<string> AllowedFeatureKeys =
        new(StringComparer.Ordinal) { "covered", "hasCharger", "chargerPowerKw" };

    public static OperationalLayoutProjectionDto Apply(
        OperationalLayoutProjectionDto dto,
        LayoutVisibility visibility,
        int maskedReservedCount,
        IReadOnlyDictionary<string, int> maskedZoneReservedCounts)
    {
        if (visibility == LayoutVisibility.Telemetry) return dto;

        return dto with
        {
            Metrics = SanitizeMetrics(dto.Metrics, maskedReservedCount, maskedZoneReservedCounts),
            Slots = dto.Slots.Select(SanitizeSlot).ToArray()
        };
    }

    private static SlotVisualStateDto SanitizeSlot(SlotVisualStateDto slot) => slot with
    {
        // BACKUP is operationally sensitive. Mask it as RESERVED while carrying the
        // pre-mask IsBookable value so masking can never turn false into true.
        ReservationState = slot.ReservationState == ReservationState.BACKUP
            ? ReservationState.RESERVED
            : slot.ReservationState,
        SessionType = null,
        OperatorContext = null,
        Features = SanitizeFeatures(slot.Features),
        IsBookable = slot.IsBookable
    };

    private static LayoutMetricsDto SanitizeMetrics(
        LayoutMetricsDto metrics,
        int maskedReservedCount,
        IReadOnlyDictionary<string, int> maskedZoneReservedCounts) => new(
        metrics.TotalPhysicalCapacity,
        metrics.OccupiedCount,
        // Sanitized callers must not be able to infer BACKUP count by diffing this value.
        maskedReservedCount,
        metrics.AvailableCarSlots,
        metrics.MotorcycleCapacity,
        metrics.ZoneCapacities.Select(zone => new ZoneCapacityDto(
            zone.ZoneId,
            zone.ZoneCode,
            zone.FloorCode,
            null,
            zone.MaxCapacity,
            zone.OccupiedCount,
            0,
            maskedZoneReservedCounts.TryGetValue(zone.ZoneCode, out var reserved) ? reserved : 0)).ToArray());

    private static JsonElement? SanitizeFeatures(JsonElement? features)
    {
        // Fail closed: non-object feature payloads are not allowed through unredacted.
        if (features is not { ValueKind: JsonValueKind.Object } value) return null;

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var property in value.EnumerateObject())
            {
                if (!AllowedFeatureKeys.Contains(property.Name)) continue;
                // Nested objects/arrays could smuggle unredacted data; only primitives survive.
                if (!IsPrimitive(property.Value.ValueKind)) continue;
                property.WriteTo(writer);
            }
            writer.WriteEndObject();
        }

        using var document = JsonDocument.Parse(buffer.ToArray());
        return document.RootElement.Clone();
    }

    private static bool IsPrimitive(JsonValueKind kind) =>
        kind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True
            or JsonValueKind.False or JsonValueKind.Null;
}
