using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using SmartParking.ParkingService.Application;
using SmartParking.ParkingService.Domain;

namespace SmartParking.ParkingService.Tests;

public sealed class OperationalLayoutContractFixtureTests
{
    private const string NormalFixture = "slots-operational-fixture.json";
    private const string StaleFixture = "slots-operational-stale-fixture.json";
    private const string SlotCodePatternText = "^[A-Za-z0-9_-]+( [A-Za-z0-9_-]+)?$";

    // The fixtures were captured at this instant; freshness is never evaluated against the wall clock.
    private static readonly DateTimeOffset FixtureClock = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly Regex SlotCodePattern = new(SlotCodePatternText, RegexOptions.CultureInvariant);

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

    [Theory]
    [InlineData(NormalFixture)]
    [InlineData(StaleFixture)]
    public void FixtureBindsToProductionProjectionDtoAndKeepsContractInvariants(string fileName)
    {
        var envelope = Load(fileName);
        var data = envelope.Data;

        // 1. Envelope + production DTO bind without throwing.
        Assert.True(envelope.Success);
        Assert.NotNull(data);

        // 10. Contract constants are carried by the payload.
        Assert.Equal(OperationalLayoutContract.Version, data.ContractVersion);
        Assert.Equal(OperationalLayoutContract.StalenessThresholdSeconds, data.StalenessThresholdSeconds);
        Assert.Equal(30, data.StalenessThresholdSeconds);

        // 2. Slot identity/count.
        Assert.Equal(48, data.Slots.Count);
        Assert.Equal(48, data.Slots.Select(s => s.SlotCode).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(48, data.Slots.Select(s => s.SlotId).Distinct().Count());

        // 3 + 4. Exact 5x4 matrix and complete enum binding.
        AssertCombinationMatrix(data.Slots);
        AssertEnumBinding(data.Slots);

        // 5. Raw mesh names are preserved and satisfy the contract pattern.
        AssertSlotCodes(data.Slots);

        // 6. Declared metrics agree with the fixture; motorcycle values are literal, not derived.
        AssertMetrics(data.Metrics);

        // 7. Bookable derivation and fail-closed states.
        AssertBookableInvariant(data.Slots);

        // 11. JsonElement payloads and nullable fields.
        AssertNullableAndJsonPayloads(data.Slots);
    }

    [Fact]
    public void FixturesDifferOnlyInGeneratedAtAndIsFreshAndFreshnessUsesFixedClock()
    {
        var normalEnvelope = Load(NormalFixture);
        var staleEnvelope = Load(StaleFixture);
        var normal = normalEnvelope.Data;
        var stale = staleEnvelope.Data;

        // 8. Freshness flags.
        Assert.True(normal.IsFresh);
        Assert.False(stale.IsFresh);

        // 9. Fixed-clock ages: 0s and 900s against a 30s threshold.
        Assert.Equal(TimeSpan.Zero, FixtureClock - normal.GeneratedAt);
        Assert.Equal(TimeSpan.FromMinutes(15), FixtureClock - stale.GeneratedAt);
        Assert.True((FixtureClock - normal.GeneratedAt).TotalSeconds <= normal.StalenessThresholdSeconds);
        Assert.True((FixtureClock - stale.GeneratedAt).TotalSeconds > stale.StalenessThresholdSeconds);

        // 8. Recursive diff between the two deserialized fixtures: exactly two paths.
        var differences = Diff(normalEnvelope, staleEnvelope);
        Assert.Equal(new[] { "data.generatedAt", "data.isFresh" },
            differences.OrderBy(path => path, StringComparer.Ordinal));
    }

    private static void AssertCombinationMatrix(IReadOnlyList<SlotVisualStateDto> slots)
    {
        var expected = new Dictionary<(PhysicalState, ReservationState), int>
        {
            [(PhysicalState.AVAILABLE, ReservationState.UNRESERVED)] = 16,
            [(PhysicalState.AVAILABLE, ReservationState.RESERVED)] = 4,
            [(PhysicalState.AVAILABLE, ReservationState.PROTECTED)] = 1,
            [(PhysicalState.AVAILABLE, ReservationState.BACKUP)] = 1,
            [(PhysicalState.OCCUPIED, ReservationState.UNRESERVED)] = 9,
            [(PhysicalState.OCCUPIED, ReservationState.RESERVED)] = 2,
            [(PhysicalState.OCCUPIED, ReservationState.PROTECTED)] = 1,
            [(PhysicalState.OCCUPIED, ReservationState.BACKUP)] = 1,
            [(PhysicalState.UNKNOWN, ReservationState.UNRESERVED)] = 1,
            [(PhysicalState.UNKNOWN, ReservationState.RESERVED)] = 1,
            [(PhysicalState.UNKNOWN, ReservationState.PROTECTED)] = 1,
            [(PhysicalState.UNKNOWN, ReservationState.BACKUP)] = 1,
            [(PhysicalState.MAINTENANCE, ReservationState.UNRESERVED)] = 2,
            [(PhysicalState.MAINTENANCE, ReservationState.RESERVED)] = 1,
            [(PhysicalState.MAINTENANCE, ReservationState.PROTECTED)] = 1,
            [(PhysicalState.MAINTENANCE, ReservationState.BACKUP)] = 1,
            [(PhysicalState.UNAVAILABLE, ReservationState.UNRESERVED)] = 1,
            [(PhysicalState.UNAVAILABLE, ReservationState.RESERVED)] = 1,
            [(PhysicalState.UNAVAILABLE, ReservationState.PROTECTED)] = 1,
            [(PhysicalState.UNAVAILABLE, ReservationState.BACKUP)] = 1
        };

        var actual = slots
            .GroupBy(slot => (slot.PhysicalState, slot.ReservationState))
            .ToDictionary(group => group.Key, group => group.Count());

        Assert.Equal(expected.Count, actual.Count);
        foreach (var (combination, expectedCount) in expected)
        {
            Assert.True(actual.ContainsKey(combination), $"Missing combination {combination}");
            Assert.Equal(expectedCount, actual[combination]);
        }
    }

    private static void AssertEnumBinding(IReadOnlyList<SlotVisualStateDto> slots)
    {
        var physicalStates = slots.Select(slot => slot.PhysicalState).ToHashSet();
        var reservationStates = slots.Select(slot => slot.ReservationState).ToHashSet();

        Assert.True(Enum.GetValues<PhysicalState>().ToHashSet().SetEquals(physicalStates));
        Assert.True(Enum.GetValues<ReservationState>().ToHashSet().SetEquals(reservationStates));
        Assert.All(slots, slot => Assert.True(Enum.IsDefined(slot.PhysicalState)));
        Assert.All(slots, slot => Assert.True(Enum.IsDefined(slot.ReservationState)));
    }

    private static void AssertSlotCodes(IReadOnlyList<SlotVisualStateDto> slots)
    {
        Assert.All(slots, slot =>
        {
            Assert.True(slot.SlotCode.Length <= 32, $"{slot.SlotCode} exceeds 32 characters.");
            Assert.Matches(SlotCodePattern, slot.SlotCode);
        });

        // Raw Blender mesh names, including duplicate-suffix spaces, must not be normalized.
        Assert.Contains("CarParkingSpace_008 2", slots.Select(slot => slot.SlotCode));
        Assert.Contains("CarParkingSpace_012 3", slots.Select(slot => slot.SlotCode));
    }

    private static void AssertMetrics(LayoutMetricsDto metrics)
    {
        Assert.Equal(48, metrics.TotalPhysicalCapacity);
        Assert.Equal(13, metrics.OccupiedCount);
        Assert.Equal(9, metrics.ReservedCount);
        Assert.Equal(16, metrics.AvailableCarSlots);

        // Motorcycle capacity is a scalar pool and is not derivable from slots[].
        Assert.Equal(20, metrics.MotorcycleCapacity.MaxCapacity);
        Assert.Equal(8, metrics.MotorcycleCapacity.OccupiedCount);
        Assert.Equal(12, metrics.MotorcycleCapacity.AvailableCount);

        Assert.Equal(2, metrics.ZoneCapacities.Count);
        var zoneA = Assert.Single(metrics.ZoneCapacities, zone => zone.ZoneCode == "Zone_A");
        Assert.Equal(24, zoneA.MaxCapacity);
        Assert.Equal(4, zoneA.OccupiedCount);
        var zoneB = Assert.Single(metrics.ZoneCapacities, zone => zone.ZoneCode == "Zone_B");
        Assert.Equal(24, zoneB.MaxCapacity);
        Assert.Equal(9, zoneB.OccupiedCount);
    }

    private static void AssertBookableInvariant(IReadOnlyList<SlotVisualStateDto> slots)
    {
        Assert.All(slots, slot =>
        {
            var expected = slot.PhysicalState == PhysicalState.AVAILABLE
                && slot.ReservationState == ReservationState.UNRESERVED;
            Assert.Equal(expected, slot.IsBookable);

            if (slot.PhysicalState == PhysicalState.UNKNOWN || slot.ReservationState == ReservationState.BACKUP)
                Assert.False(slot.IsBookable);
        });
    }

    private static void AssertNullableAndJsonPayloads(IReadOnlyList<SlotVisualStateDto> slots)
    {
        Assert.All(slots, slot =>
        {
            Assert.True(slot.Coordinates3D.HasValue, $"{slot.SlotCode} is missing coordinates3d.");
            Assert.Equal(JsonValueKind.Object, slot.Coordinates3D.GetValueOrDefault().ValueKind);
            Assert.True(slot.Features.HasValue, $"{slot.SlotCode} is missing features.");
            Assert.Equal(JsonValueKind.Object, slot.Features.GetValueOrDefault().ValueKind);
        });

        Assert.Equal(24, slots.Count(slot => slot.SessionType is null));
        Assert.Equal(34, slots.Count(slot => slot.OperatorContext is null));
        Assert.Equal(44, slots.Count(slot => slot.Features is JsonElement features
            && features.TryGetProperty("chargerPowerKw", out var charger)
            && charger.ValueKind == JsonValueKind.Null));

        Assert.All(slots.Where(slot => slot.OperatorContext is not null), slot =>
            Assert.Equal(JsonValueKind.Object, slot.OperatorContext.GetValueOrDefault().ValueKind));
    }

    private static OperationalLayoutEnvelope Load(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", fileName);
        Assert.True(File.Exists(path), $"Fixture was not copied to test output: {path}");

        var json = File.ReadAllText(path);
        var envelope = JsonSerializer.Deserialize<OperationalLayoutEnvelope>(json, ReadOptions)
            ?? throw new InvalidOperationException($"Could not deserialize fixture: {path}");

        Assert.True(envelope.Success);
        Assert.NotNull(envelope.Data);
        return envelope;
    }

    private static IReadOnlyList<string> Diff(
        OperationalLayoutEnvelope normal,
        OperationalLayoutEnvelope stale)
    {
        var left = JsonSerializer.SerializeToNode(normal, RoundTripOptions)
            ?? throw new InvalidOperationException("Could not serialize the normal fixture envelope.");
        var right = JsonSerializer.SerializeToNode(stale, RoundTripOptions)
            ?? throw new InvalidOperationException("Could not serialize the stale fixture envelope.");

        var differences = new List<string>();
        DiffNode(left, right, string.Empty, differences);
        return differences;
    }

    private static void DiffNode(JsonNode? left, JsonNode? right, string path, List<string> differences)
    {
        if (left is null || right is null)
        {
            if (!JsonNode.DeepEquals(left, right)) differences.Add(path);
            return;
        }

        if (left is JsonObject leftObject && right is JsonObject rightObject)
        {
            var keys = leftObject.Select(pair => pair.Key)
                .Concat(rightObject.Select(pair => pair.Key))
                .Distinct(StringComparer.Ordinal);

            foreach (var key in keys)
            {
                var childPath = string.IsNullOrEmpty(path) ? key : $"{path}.{key}";
                DiffNode(leftObject[key], rightObject[key], childPath, differences);
            }

            return;
        }

        if (left is JsonArray leftArray && right is JsonArray rightArray)
        {
            if (leftArray.Count != rightArray.Count)
            {
                differences.Add(path);
                return;
            }

            for (var index = 0; index < leftArray.Count; index++)
                DiffNode(leftArray[index], rightArray[index], $"{path}[{index}]", differences);

            return;
        }

        if (!JsonNode.DeepEquals(left, right)) differences.Add(path);
    }
}

public sealed record OperationalLayoutEnvelope(bool Success, OperationalLayoutProjectionDto Data);
