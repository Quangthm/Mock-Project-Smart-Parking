using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SmartParking.ParkingService.Application;
using SmartParking.ParkingService.Domain;

namespace SmartParking.ParkingService.Tests;

public sealed class OperationalLayoutRedactionTests
{
    private const string NormalFixture = "slots-operational-fixture.json";

    private static readonly string[] AllowedFeatureKeys = { "covered", "hasCharger", "chargerPowerKw" };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    private static readonly JsonSerializerOptions RoundTripOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    [Fact]
    public void TelemetryVisibilityReturnsTheSameProjectionWithoutRedaction()
    {
        var dto = BuildProjection();

        var result = OperationalLayoutRedaction.Apply(dto, LayoutVisibility.Telemetry, 0, EmptyCounts);

        Assert.Same(dto, result);
        AssertDeepEqual(dto, result);
        Assert.Equal(dto.Slots.Select(slot => slot.SlotId), result.Slots.Select(slot => slot.SlotId));
        Assert.Equal(ReservationState.BACKUP, result.Slots[0].ReservationState);
        Assert.Equal<SessionType?>(SessionType.STANDARD, result.Slots[0].SessionType);
        Assert.True(result.Slots[0].OperatorContext.HasValue);
        Assert.Equal("North", result.Metrics.ZoneCapacities[0].Name);
        Assert.Equal(3, result.Metrics.ZoneCapacities[0].AllocatedSlotCount);
    }

    [Fact]
    public void SanitizedMasksBackupAndClearsSessionAndOperatorContext()
    {
        var dto = BuildProjection();

        var result = Sanitize(dto);

        Assert.Equal(new[]
        {
            ReservationState.RESERVED,
            ReservationState.RESERVED,
            ReservationState.PROTECTED,
            ReservationState.UNRESERVED,
            ReservationState.UNRESERVED
        }, result.Slots.Select(slot => slot.ReservationState));

        Assert.Equal(0, result.Slots.Count(slot => slot.ReservationState == ReservationState.BACKUP));
        Assert.All(result.Slots, slot => Assert.True(slot.SessionType is null));
        Assert.All(result.Slots, slot => Assert.False(slot.OperatorContext.HasValue));
    }

    [Fact]
    public void SanitizedRebuildsFeaturesUsingOnlyAllowedKeys()
    {
        var dto = BuildProjection();

        var result = Sanitize(dto);

        for (var index = 0; index < dto.Slots.Count; index++)
            AssertFeaturesSanitized(dto.Slots[index].Features, result.Slots[index].Features);

        Assert.DoesNotContain("internalNotes", FeatureNames(result.Slots[0].Features));
        Assert.DoesNotContain("unknownKey", FeatureNames(result.Slots[0].Features));
        Assert.DoesNotContain("internalNotes", FeatureNames(result.Slots[1].Features));
        Assert.DoesNotContain("extra", FeatureNames(result.Slots[3].Features));
    }

    [Fact]
    public void SanitizedMasksZoneMetadataAndUsesMaskedReservedMetrics()
    {
        var dto = BuildProjection();
        var before = dto.Metrics;

        var result = Sanitize(dto);
        var after = result.Metrics;

        Assert.Equal(before.TotalPhysicalCapacity, after.TotalPhysicalCapacity);
        Assert.Equal(before.OccupiedCount, after.OccupiedCount);
        // BuildProjection has 1 genuine RESERVED + 1 masked BACKUP slot.
        Assert.Equal(2, after.ReservedCount);
        Assert.Equal(before.AvailableCarSlots, after.AvailableCarSlots);
        Assert.Equal(before.MotorcycleCapacity, after.MotorcycleCapacity);

        Assert.Equal(before.ZoneCapacities.Count, after.ZoneCapacities.Count);
        var beforeZone = before.ZoneCapacities[0];
        var afterZone = after.ZoneCapacities[0];
        Assert.Equal(beforeZone.ZoneId, afterZone.ZoneId);
        Assert.Equal(beforeZone.ZoneCode, afterZone.ZoneCode);
        Assert.Equal(beforeZone.FloorCode, afterZone.FloorCode);
        Assert.Equal(beforeZone.MaxCapacity, afterZone.MaxCapacity);
        Assert.Equal(beforeZone.OccupiedCount, afterZone.OccupiedCount);
        Assert.Equal(2, afterZone.ZoneOnlyReservedCount);
        Assert.Null(afterZone.Name);
        Assert.Equal(0, afterZone.AllocatedSlotCount);
    }

    [Fact]
    public void SanitizedPreservesBookableIdentityOrderAndUnrelatedFields()
    {
        var dto = BuildProjection();

        var result = Sanitize(dto);

        // A BACKUP slot that was not bookable stays false after it is displayed as RESERVED.
        Assert.False(result.Slots[0].IsBookable);
        Assert.False(result.Slots[1].IsBookable);
        Assert.True(result.Slots[3].IsBookable);

        Assert.Equal(dto.Slots.Count, result.Slots.Count);
        Assert.Equal(dto.Slots.Select(slot => slot.SlotId), result.Slots.Select(slot => slot.SlotId));

        for (var index = 0; index < dto.Slots.Count; index++)
        {
            var before = dto.Slots[index];
            var after = result.Slots[index];
            Assert.Equal(before.SlotCode, after.SlotCode);
            Assert.Equal(before.FloorId, after.FloorId);
            Assert.Equal(before.FloorCode, after.FloorCode);
            Assert.Equal(before.ZoneCode, after.ZoneCode);
            Assert.Equal(before.RowCode, after.RowCode);
            Assert.Equal(before.SupportedVehicleType, after.SupportedVehicleType);
            Assert.Equal(before.SlotType, after.SlotType);
            Assert.Equal(before.PhysicalState, after.PhysicalState);
            Assert.Equal(before.IsEvCharging, after.IsEvCharging);
            Assert.Equal(before.IsBookable, after.IsBookable);
            Assert.Equal(before.Coordinates3D?.GetRawText(), after.Coordinates3D?.GetRawText());
            Assert.Equal(before.UpdatedAt, after.UpdatedAt);
        }

        Assert.Equal(dto.SiteId, result.SiteId);
        Assert.Equal(dto.SiteCode, result.SiteCode);
        Assert.Equal(dto.SiteName, result.SiteName);
        Assert.Equal(dto.ContractVersion, result.ContractVersion);
        Assert.Equal(dto.GeneratedAt, result.GeneratedAt);
        Assert.Equal(dto.IsFresh, result.IsFresh);
        Assert.Equal(dto.StalenessThresholdSeconds, result.StalenessThresholdSeconds);
        Assert.Equal(dto.Floors, result.Floors);
    }

    [Fact]
    public void SanitizedDoesNotMutateTheSourceProjection()
    {
        var dto = BuildProjection();

        _ = Sanitize(dto);

        Assert.Equal(ReservationState.BACKUP, dto.Slots[0].ReservationState);
        Assert.Equal<SessionType?>(SessionType.STANDARD, dto.Slots[0].SessionType);
        Assert.True(dto.Slots[0].OperatorContext.HasValue);
        Assert.Contains("internalNotes", FeatureNames(dto.Slots[0].Features));
        Assert.Equal("North", dto.Metrics.ZoneCapacities[0].Name);
        Assert.Equal(3, dto.Metrics.ZoneCapacities[0].AllocatedSlotCount);
        Assert.Equal(1, dto.Metrics.ReservedCount);
    }

    [Fact]
    public void SanitizedRealFixtureMasksBackupAndUsesMaskedReservedMetric()
    {
        var original = LoadFixture(NormalFixture);

        // Observed from the real fixture: 5 BACKUP slots, not 2 as the task packet guessed.
        var backupBefore = original.Slots.Count(slot => slot.ReservationState == ReservationState.BACKUP);
        var reservedBefore = original.Slots.Count(slot => slot.ReservationState == ReservationState.RESERVED);
        Assert.Equal(5, backupBefore);
        Assert.Equal(9, reservedBefore);
        Assert.Equal(24, original.Slots.Count(slot => slot.SessionType is not null));
        Assert.Equal(14, original.Slots.Count(slot => slot.OperatorContext.HasValue));

        var sanitized = Sanitize(original);

        // A7: same 48 SlotIds in the same order.
        Assert.Equal(48, sanitized.Slots.Count);
        Assert.Equal(original.Slots.Select(slot => slot.SlotId), sanitized.Slots.Select(slot => slot.SlotId));

        // A7: no sensitive reservation/session/operator payload survives.
        Assert.Equal(0, sanitized.Slots.Count(slot => slot.ReservationState == ReservationState.BACKUP));
        Assert.Equal(0, sanitized.Slots.Count(slot => slot.SessionType is not null));
        Assert.Equal(0, sanitized.Slots.Count(slot => slot.OperatorContext.HasValue));
        Assert.All(sanitized.Slots, slot =>
            Assert.All(FeatureNames(slot.Features), name => Assert.Contains(name, AllowedFeatureKeys)));

        // The masked slot-level RESERVED count is 9 genuine + 5 masked BACKUP slots = 14.
        Assert.Equal(14, sanitized.Slots.Count(slot => slot.ReservationState == ReservationState.RESERVED));

        // Sanitized metrics use the same post-mask count, so BACKUP cannot be inferred by diffing.
        Assert.Equal(14, sanitized.Metrics.ReservedCount);
        Assert.Equal(original.Metrics.OccupiedCount, sanitized.Metrics.OccupiedCount);
        Assert.Equal(original.Metrics.AvailableCarSlots, sanitized.Metrics.AvailableCarSlots);

        // Telemetry keeps the truthful RESERVED-only metric.
        var telemetry = OperationalLayoutRedaction.Apply(original, LayoutVisibility.Telemetry, 0, EmptyCounts);
        Assert.Equal(9, telemetry.Metrics.ReservedCount);
        Assert.Equal(original.Slots.Select(slot => slot.IsBookable),
            sanitized.Slots.Select(slot => slot.IsBookable));
        Assert.Equal(original.Slots.Select(slot => slot.PhysicalState),
            sanitized.Slots.Select(slot => slot.PhysicalState));
        Assert.All(sanitized.Metrics.ZoneCapacities, zone => Assert.Null(zone.Name));
        Assert.All(sanitized.Metrics.ZoneCapacities, zone => Assert.Equal(0, zone.AllocatedSlotCount));

        // Redaction is non-destructive: the source still carries the sensitive values.
        Assert.Equal(5, original.Slots.Count(slot => slot.ReservationState == ReservationState.BACKUP));
        Assert.Equal(14, original.Slots.Count(slot => slot.OperatorContext.HasValue));
    }

    [Fact]
    public void SanitizedUsesProvidedMaskedReservedCountNotTheDtoValue()
    {
        var source = BuildProjection();
        var dto = source with { Metrics = source.Metrics with { ReservedCount = 9 } };
        var maskedZoneCounts = new Dictionary<string, int>(StringComparer.Ordinal) { ["Zone_A"] = 10 };

        var sanitized = OperationalLayoutRedaction.Apply(
            dto, LayoutVisibility.Sanitized, 14, maskedZoneCounts);
        var telemetry = OperationalLayoutRedaction.Apply(
            dto, LayoutVisibility.Telemetry, 14, maskedZoneCounts);

        Assert.Equal(14, sanitized.Metrics.ReservedCount);
        Assert.Equal(9, telemetry.Metrics.ReservedCount);
        Assert.Same(dto, telemetry);
    }

    [Fact]
    public void SanitizedZoneOnlyReservedCountUsesProvidedPerZoneCounts()
    {
        var source = BuildProjection();
        var zoneA = source.Metrics.ZoneCapacities[0];
        var zoneB = new ZoneCapacityDto(
            Guid.Parse("8b9c0d1e-2f3a-4b5c-6d7e-8f9a0b1c2d3e"),
            "Zone_B", "Floor_G", "South", 7, 3, 2, 99);
        var zoneC = new ZoneCapacityDto(
            Guid.Parse("9c0d1e2f-3a4b-5c6d-7e8f-9a0b1c2d3e4f"),
            "Zone_C", "Floor_G", "East", 9, 4, 5, 42);
        var dto = source with
        {
            Metrics = source.Metrics with { ZoneCapacities = new[] { zoneA, zoneB, zoneC } }
        };
        var maskedZoneCounts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Zone_A"] = 10,
            ["Zone_B"] = 4
        };

        var result = OperationalLayoutRedaction.Apply(
            dto, LayoutVisibility.Sanitized, 14, maskedZoneCounts);

        var resultA = Assert.Single(result.Metrics.ZoneCapacities, zone => zone.ZoneCode == "Zone_A");
        var resultB = Assert.Single(result.Metrics.ZoneCapacities, zone => zone.ZoneCode == "Zone_B");
        var resultC = Assert.Single(result.Metrics.ZoneCapacities, zone => zone.ZoneCode == "Zone_C");

        Assert.Equal(10, resultA.ZoneOnlyReservedCount);
        Assert.Equal(4, resultB.ZoneOnlyReservedCount);
        Assert.Equal(0, resultC.ZoneOnlyReservedCount);
        Assert.All(result.Metrics.ZoneCapacities, zone =>
        {
            Assert.Null(zone.Name);
            Assert.Equal(0, zone.AllocatedSlotCount);
        });

        Assert.Equal(zoneA.ZoneId, resultA.ZoneId);
        Assert.Equal(zoneB.ZoneId, resultB.ZoneId);
        Assert.Equal(zoneC.ZoneId, resultC.ZoneId);
        Assert.Equal(zoneA.MaxCapacity, resultA.MaxCapacity);
        Assert.Equal(zoneB.OccupiedCount, resultB.OccupiedCount);
        Assert.Equal(zoneC.OccupiedCount, resultC.OccupiedCount);
    }

    [Fact]
    public void SanitizedMaskedCountsAreIndependentOfSlotFiltering()
    {
        var source = BuildProjection();
        // Simulate vehicleType/floor filtering: only two of the five slots are displayed.
        var filtered = source with { Slots = source.Slots.Take(2).ToArray() };
        var maskedZoneCounts = new Dictionary<string, int>(StringComparer.Ordinal) { ["Zone_A"] = 10 };

        var result = OperationalLayoutRedaction.Apply(
            filtered, LayoutVisibility.Sanitized, 14, maskedZoneCounts);

        var visibleReserved = result.Slots.Count(slot =>
            slot.ReservationState == ReservationState.RESERVED);

        Assert.Equal(2, result.Slots.Count);
        Assert.Equal(2, visibleReserved);
        // The metric comes from the provided full-site count, never from the filtered slots[].
        Assert.Equal(14, result.Metrics.ReservedCount);
        Assert.NotEqual(visibleReserved, result.Metrics.ReservedCount);
        Assert.Equal(10, result.Metrics.ZoneCapacities[0].ZoneOnlyReservedCount);
    }

    [Fact]
    public void SanitizedReservedCountMatchesPostMaskSlotViewWhenUnfiltered()
    {
        var original = LoadFixture(NormalFixture);
        var genuineReserved = original.Slots.Count(slot =>
            slot.ReservationState == ReservationState.RESERVED);
        var backup = original.Slots.Count(slot =>
            slot.ReservationState == ReservationState.BACKUP);

        var sanitized = Sanitize(original);
        var postMaskView = sanitized.Slots.Count(slot =>
            slot.ReservationState == ReservationState.RESERVED);

        Assert.Equal(9, genuineReserved);
        Assert.Equal(5, backup);
        Assert.Equal(14, postMaskView);
        Assert.Equal(postMaskView, sanitized.Metrics.ReservedCount);
        Assert.Equal(genuineReserved + backup, sanitized.Metrics.ReservedCount);
    }

    [Fact]
    public void SanitizedFeaturesBecomeNullWhenNotAJsonObject()
    {
        var source = BuildProjection();
        var template = source.Slots[4];
        var features = new JsonElement?[]
        {
            Json("[1,2]"),
            Json("\"secret\""),
            Json("7"),
            Json("true"),
            null,
            Json("""{"covered":true}""")
        };
        var slots = features.Select((value, index) => template with
        {
            SlotId = Guid.Parse($"00000000-0000-0000-0000-{index + 10:D12}"),
            Features = value
        }).ToArray();
        var dto = source with { Slots = slots };

        var result = OperationalLayoutRedaction.Apply(
            dto, LayoutVisibility.Sanitized, 0, EmptyCounts);

        for (var index = 0; index < 5; index++)
            Assert.False(result.Slots[index].Features.HasValue);

        Assert.True(result.Slots[5].Features.HasValue);
        var objectFeatures = result.Slots[5].Features.GetValueOrDefault();
        Assert.Equal(JsonValueKind.Object, objectFeatures.ValueKind);
        Assert.True(objectFeatures.GetProperty("covered").GetBoolean());
    }

    [Fact]
    public void SanitizedDropsNestedOrArrayValuesUnderAllowedKeys()
    {
        var source = BuildProjection();
        var template = source.Slots[4];
        var nested = template with
        {
            SlotId = Guid.Parse("00000000-0000-0000-0000-000000000011"),
            Features = Json("""{"covered":{"internalNotes":"secret"},"hasCharger":true,"chargerPowerKw":[1,2]}""")
        };
        var primitive = template with
        {
            SlotId = Guid.Parse("00000000-0000-0000-0000-000000000012"),
            Features = Json("""{"covered":true,"hasCharger":false,"chargerPowerKw":7.5}""")
        };
        var dto = source with { Slots = new[] { nested, primitive } };

        var result = OperationalLayoutRedaction.Apply(
            dto, LayoutVisibility.Sanitized, 0, EmptyCounts);

        var sanitizedNested = result.Slots[0].Features.GetValueOrDefault();
        Assert.Equal(JsonValueKind.Object, sanitizedNested.ValueKind);
        Assert.Equal(new[] { "hasCharger" },
            sanitizedNested.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.True(sanitizedNested.GetProperty("hasCharger").GetBoolean());
        Assert.False(sanitizedNested.TryGetProperty("covered", out _));
        Assert.False(sanitizedNested.TryGetProperty("chargerPowerKw", out _));

        var sanitizedPrimitive = result.Slots[1].Features.GetValueOrDefault();
        Assert.Equal(7.5, sanitizedPrimitive.GetProperty("chargerPowerKw").GetDouble());
        Assert.True(sanitizedPrimitive.GetProperty("covered").GetBoolean());
        Assert.False(sanitizedPrimitive.GetProperty("hasCharger").GetBoolean());
    }

    [Fact]
    public void TelemetryPreservesFeaturesVerbatim()
    {
        var source = BuildProjection();
        var features = Json(
            """{"covered":{"internalNotes":"secret"},"hasCharger":true,"chargerPowerKw":[1,2]}""");
        var dto = source with
        {
            Slots = new[] { source.Slots[0] with { Features = features } }
        };

        var result = OperationalLayoutRedaction.Apply(
            dto, LayoutVisibility.Telemetry, 0, EmptyCounts);

        Assert.Same(dto, result);
        Assert.True(result.Slots[0].Features.HasValue);
        Assert.Equal(features.GetRawText(),
            result.Slots[0].Features.GetValueOrDefault().GetRawText());
    }

    private static void AssertFeaturesSanitized(JsonElement? original, JsonElement? sanitized)
    {
        if (original is null)
        {
            Assert.False(sanitized.HasValue);
            return;
        }

        var source = original.GetValueOrDefault();
        Assert.Equal(JsonValueKind.Object, source.ValueKind);
        Assert.True(sanitized.HasValue);
        var result = sanitized.GetValueOrDefault();
        Assert.Equal(JsonValueKind.Object, result.ValueKind);

        Assert.All(result.EnumerateObject().Select(property => property.Name),
            name => Assert.Contains(name, AllowedFeatureKeys));

        foreach (var name in AllowedFeatureKeys)
        {
            var sourceHas = source.TryGetProperty(name, out var sourceValue);
            var resultHas = result.TryGetProperty(name, out var resultValue);
            Assert.Equal(sourceHas, resultHas);
            if (sourceHas) Assert.Equal(sourceValue.GetRawText(), resultValue.GetRawText());
        }
    }

    private static IEnumerable<string> FeatureNames(JsonElement? features)
        => features is { ValueKind: JsonValueKind.Object } value
            ? value.EnumerateObject().Select(property => property.Name)
            : Enumerable.Empty<string>();

    private static void AssertDeepEqual(
        OperationalLayoutProjectionDto expected,
        OperationalLayoutProjectionDto actual)
    {
        var expectedNode = JsonSerializer.SerializeToNode(expected, RoundTripOptions)
            ?? throw new InvalidOperationException("Could not serialize the expected projection.");
        var actualNode = JsonSerializer.SerializeToNode(actual, RoundTripOptions)
            ?? throw new InvalidOperationException("Could not serialize the actual projection.");

        Assert.True(JsonNode.DeepEquals(expectedNode, actualNode));
    }

    private static OperationalLayoutProjectionDto LoadFixture(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", fileName);
        Assert.True(File.Exists(path), $"Fixture was not copied to test output: {path}");

        var json = File.ReadAllText(path);
        var envelope = JsonSerializer.Deserialize<OperationalLayoutEnvelope>(json, ReadOptions)
            ?? throw new InvalidOperationException($"Could not deserialize fixture: {path}");

        Assert.True(envelope.Success);
        Assert.NotNull(envelope.Data);
        return envelope.Data;
    }

    private static readonly IReadOnlyDictionary<string, int> EmptyCounts = new Dictionary<string, int>();

    private static OperationalLayoutProjectionDto Sanitize(OperationalLayoutProjectionDto dto)
        => OperationalLayoutRedaction.Apply(
            dto,
            LayoutVisibility.Sanitized,
            dto.Slots.Count(slot =>
                slot.ReservationState is ReservationState.RESERVED or ReservationState.BACKUP),
            dto.Slots
                .Where(slot => slot.ZoneCode is not null &&
                    slot.ReservationState is ReservationState.RESERVED or ReservationState.BACKUP)
                .GroupBy(slot => slot.ZoneCode!, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal));

    private static OperationalLayoutProjectionDto BuildProjection()
    {
        var siteId = Guid.Parse("f7d3a2b1-5e8c-4a3d-9f1e-2c8b7a6d5e4f");
        var floorId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var zoneId = Guid.Parse("7a8b9c0d-1e2f-3a4b-5c6d-7e8f9a0b1c2d");
        var updatedAt = new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

        var slots = new SlotVisualStateDto[]
        {
            new(Guid.Parse("00000000-0000-0000-0000-000000000001"), "CarParkingSpace_001", floorId, "Floor_G",
                "Zone_A", "Row_01", VehicleType.CAR, SlotType.EV, PhysicalState.AVAILABLE,
                ReservationState.BACKUP, SessionType.STANDARD, false, false, Json("""{"x":1,"y":0,"z":-1}"""),
                Json("""{"covered":true,"hasCharger":true,"chargerPowerKw":22,"internalNotes":"do-not-leak","unknownKey":[1,2]}"""),
                Json("""{"activeSessionId":"e5f6a7b8-c9d0-1e2f-3a4b-5c6d7e8f9a0b","maskedPlate":"29B-*****.01","internalNotes":"secret"}"""),
                updatedAt),
            new(Guid.Parse("00000000-0000-0000-0000-000000000002"), "CarParkingSpace_002 2", floorId, "Floor_G",
                "Zone_A", "Row_01", VehicleType.CAR, SlotType.STANDARD, PhysicalState.OCCUPIED,
                ReservationState.RESERVED, SessionType.VIP, false, false, Json("""{"x":2,"y":0,"z":-1}"""),
                Json("""{"covered":false,"hasCharger":false,"chargerPowerKw":null,"internalNotes":"drop-me"}"""),
                null, updatedAt),
            new(Guid.Parse("00000000-0000-0000-0000-000000000003"), "CarParkingSpace_003", floorId, "Floor_G",
                "Zone_A", "Row_02", VehicleType.CAR, SlotType.DISABLED, PhysicalState.UNKNOWN,
                ReservationState.PROTECTED, null, false, false, Json("""{"x":3,"y":0,"z":-1}"""),
                Json("""{"covered":true,"hasCharger":false}"""),
                Json("""{"activeSessionId":null,"maskedPlate":null,"approachingReservation":null}"""),
                updatedAt),
            new(Guid.Parse("00000000-0000-0000-0000-000000000004"), "CarParkingSpace_004", floorId, "Floor_G",
                "Zone_A", "Row_02", VehicleType.CAR, SlotType.VIP, PhysicalState.AVAILABLE,
                ReservationState.UNRESERVED, null, false, true, Json("""{"x":4,"y":0,"z":-1}"""),
                Json("""{"covered":false,"hasCharger":true,"chargerPowerKw":11,"extra":"drop-me"}"""),
                Json("""{"activeSessionId":"e5f6a7b8-c9d0-1e2f-3a4b-5c6d7e8f9a0b","maskedPlate":"29B-*****.04"}"""),
                updatedAt),
            new(Guid.Parse("00000000-0000-0000-0000-000000000005"), "CarParkingSpace_005", floorId, "Floor_G",
                "Zone_A", "Row_02", VehicleType.CAR, SlotType.STANDARD, PhysicalState.AVAILABLE,
                ReservationState.UNRESERVED, null, false, true, Json("""{"x":5,"y":0,"z":-1}"""),
                null, null, updatedAt)
        };

        return new OperationalLayoutProjectionDto(
            siteId,
            "SITE_TEST_01",
            "Contract Test Site",
            OperationalLayoutContract.Version,
            updatedAt,
            true,
            OperationalLayoutContract.StalenessThresholdSeconds,
            new LayoutMetricsDto(
                5,
                2,
                1,
                2,
                new MotorcycleCapacityDto(10, 3, 7),
                new[] { new ZoneCapacityDto(zoneId, "Zone_A", "Floor_G", "North", 5, 2, 3, 1) }),
            new[] { new FloorProjectionDto(floorId, "Floor_G", "Ground", 1, 5) },
            slots);
    }

    private static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
