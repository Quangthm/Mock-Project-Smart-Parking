using System.Text.Json;
using SmartParking.PaymentService.Application.Common.Extensions;
using SmartParking.PaymentService.Domain.Pricing;

namespace SmartParking.PaymentService.Tests.Domain;

public class ParkingPricingCalculatorTests
{
    private readonly ParkingPricingCalculator _calculator = new();
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    private static PricingPolicySnapshot CreateStandardPolicy(
        int graceMinutes = 15,
        long dayRate = 20000L,
        long nightRate = 30000L,
        MonetaryRoundingMode roundingMode = MonetaryRoundingMode.HALF_UP_WHOLE_VND,
        string timeZoneId = "Asia/Ho_Chi_Minh",
        PricingVehicleType vehicleType = PricingVehicleType.CAR)
    {
        return new PricingPolicySnapshot(
            TariffId: Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7"),
            TariffName: "Biểu giá Ô tô Tiêu chuẩn 2026",
            PolicyVersion: 1,
            VehicleType: vehicleType,
            FreeParkingGraceMinutes: graceMinutes,
            TimeBlocks: new List<TimeBlockRule>
            {
                new("DAY_SHIFT", new TimeOnly(6, 0), new TimeOnly(18, 0), dayRate),
                new("NIGHT_SHIFT", new TimeOnly(18, 0), new TimeOnly(6, 0), nightRate)
            },
            TimeZoneId: timeZoneId,
            RoundingMode: roundingMode
        );
    }

    private static PricingCalculationRequest CreateRequest(
        DateTimeOffset entry,
        DateTimeOffset exit,
        PricingPolicySnapshot policy,
        PricingVehicleType vehicleType = PricingVehicleType.CAR)
    {
        return new PricingCalculationRequest(
            TenantId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            SiteId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            VehicleType: vehicleType,
            EntryTime: entry,
            ExitTime: exit,
            Policy: policy
        );
    }

    [Fact]
    public void TC_PRICE_01_UnderGracePeriod_ShouldBeFree()
    {
        var entry = new DateTimeOffset(2026, 10, 12, 8, 0, 0, VietnamOffset);
        var exit = new DateTimeOffset(2026, 10, 12, 8, 10, 0, VietnamOffset);
        var policy = CreateStandardPolicy(graceMinutes: 15);
        var request = CreateRequest(entry, exit, policy);

        var result = _calculator.Calculate(request);

        Assert.True(result.IsFreeStay);
        Assert.Equal(0L, result.FinalAmountVnd);
        Assert.Equal(0m, result.BilledMinutes);
        Assert.Equal(10m, result.TotalParkedMinutes);
        Assert.Equal(10, result.InvoiceRoundedMinutes);
        Assert.Empty(result.Segments);
    }

    [Fact]
    public void TC_PRICE_02_ExactGracePeriod_ShouldBeFree()
    {
        var entry = new DateTimeOffset(2026, 10, 12, 8, 0, 0, VietnamOffset);
        var exit = new DateTimeOffset(2026, 10, 12, 8, 15, 0, VietnamOffset);
        var policy = CreateStandardPolicy(graceMinutes: 15);
        var request = CreateRequest(entry, exit, policy);

        var result = _calculator.Calculate(request);

        Assert.True(result.IsFreeStay);
        Assert.Equal(0L, result.FinalAmountVnd);
        Assert.Equal(0m, result.BilledMinutes);
        Assert.Equal(15m, result.TotalParkedMinutes);
        Assert.Equal(15, result.InvoiceRoundedMinutes);
        Assert.Empty(result.Segments);
    }

    [Fact]
    public void TC_PRICE_03_StandardDaytime_ShouldChargeAccurately()
    {
        var entry = new DateTimeOffset(2026, 10, 12, 8, 0, 0, VietnamOffset);
        var exit = new DateTimeOffset(2026, 10, 12, 10, 0, 0, VietnamOffset);
        var policy = CreateStandardPolicy(graceMinutes: 15);
        var request = CreateRequest(entry, exit, policy);

        var result = _calculator.Calculate(request);

        Assert.False(result.IsFreeStay);
        Assert.Equal(40000L, result.FinalAmountVnd); // 2h * 20,000 = 40,000 VND
        Assert.Equal(40000m, result.UnroundedTotalVnd);
        Assert.Equal(120m, result.TotalParkedMinutes);
        Assert.Equal(120, result.InvoiceRoundedMinutes);
        Assert.Equal(120m, result.BilledMinutes);
        Assert.Single(result.Segments);
        Assert.Equal("DAY_SHIFT", result.Segments[0].BlockCode);
        Assert.Equal(120m, result.Segments[0].OverlapMinutes);
        Assert.Equal(20000L, result.Segments[0].RatePerHourVnd);
        Assert.Equal(40000m, result.Segments[0].RawChargeVnd);
    }

    [Fact]
    public void TC_PRICE_04_DayAndEveningBoundaryCrossing_ShouldSplitAndSum()
    {
        // 16:30 to 19:00: 90 mins day (30,000) + 60 mins night (30,000) = 60,000
        var entry = new DateTimeOffset(2026, 10, 12, 16, 30, 0, VietnamOffset);
        var exit = new DateTimeOffset(2026, 10, 12, 19, 0, 0, VietnamOffset);
        var policy = CreateStandardPolicy(graceMinutes: 15);
        var request = CreateRequest(entry, exit, policy);

        var result = _calculator.Calculate(request);

        Assert.False(result.IsFreeStay);
        Assert.Equal(60000L, result.FinalAmountVnd);
        Assert.Equal(60000m, result.UnroundedTotalVnd);
        Assert.Equal(150m, result.TotalParkedMinutes);
        Assert.Equal(150, result.InvoiceRoundedMinutes);
        Assert.Equal(150m, result.BilledMinutes);
        Assert.Equal(2, result.Segments.Count);

        var seg1 = result.Segments[0];
        Assert.Equal(1, seg1.SegmentIndex);
        Assert.Equal("DAY_SHIFT", seg1.BlockCode);
        Assert.Equal(90m, seg1.OverlapMinutes);
        Assert.Equal(20000L, seg1.RatePerHourVnd);
        Assert.Equal(30000m, seg1.RawChargeVnd);

        var seg2 = result.Segments[1];
        Assert.Equal(2, seg2.SegmentIndex);
        Assert.Equal("NIGHT_SHIFT", seg2.BlockCode);
        Assert.Equal(60m, seg2.OverlapMinutes);
        Assert.Equal(30000L, seg2.RatePerHourVnd);
        Assert.Equal(30000m, seg2.RawChargeVnd);
    }

    [Fact]
    public void TC_PRICE_05_MidnightCrossing_WithRounding_ShouldRoundHalfUpAtEnd()
    {
        // 23:40 to 00:20 at 25,000 VND/hr -> 20 mins (8333.33) + 20 mins (8333.33) = 16,666.67 -> 16,667 VND
        var entry = new DateTimeOffset(2026, 10, 12, 23, 40, 0, VietnamOffset);
        var exit = new DateTimeOffset(2026, 10, 13, 0, 20, 0, VietnamOffset);
        var policy = CreateStandardPolicy(graceMinutes: 15, nightRate: 25000L);
        var request = CreateRequest(entry, exit, policy);

        var result = _calculator.Calculate(request);

        Assert.False(result.IsFreeStay);
        Assert.Equal(16667L, result.FinalAmountVnd);
        Assert.Equal(40m, result.TotalParkedMinutes);
        Assert.Equal(40, result.InvoiceRoundedMinutes);
        Assert.Equal(2, result.Segments.Count);

        Assert.Equal(DateOnly.FromDateTime(entry.DateTime), result.Segments[0].LocalDate);
        Assert.Equal(DateOnly.FromDateTime(exit.DateTime), result.Segments[1].LocalDate);
    }

    [Fact]
    public void TC_PRICE_06_MultiDayStay_Over48Hours_ShouldCalculateAllDays()
    {
        var entry = new DateTimeOffset(2026, 10, 10, 10, 0, 0, VietnamOffset);
        var exit = new DateTimeOffset(2026, 10, 12, 12, 0, 0, VietnamOffset); // 50 hours
        var policy = CreateStandardPolicy(graceMinutes: 15);
        var request = CreateRequest(entry, exit, policy);

        var result = _calculator.Calculate(request);

        Assert.False(result.IsFreeStay);
        Assert.True(result.FinalAmountVnd > 0L);
        Assert.Equal(3000m, result.TotalParkedMinutes);
        Assert.Equal(3000, result.InvoiceRoundedMinutes);
        Assert.Equal(result.TotalParkedMinutes, result.Segments.Sum(s => s.OverlapMinutes));
    }

    [Theory]
    [InlineData(12.4, 12L)]
    [InlineData(12.5, 13L)]
    [InlineData(12.6, 13L)]
    public void TC_PRICE_07_RoundingMode_HALF_UP_WHOLE_VND(decimal unrounded, long expected)
    {
        var actual = ParkingPricingCalculator.ApplyRounding(unrounded, MonetaryRoundingMode.HALF_UP_WHOLE_VND);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(12.0, 12L)]
    [InlineData(12.1, 13L)]
    [InlineData(12.9, 13L)]
    public void TC_PRICE_08_RoundingMode_CEILING_WHOLE_VND(decimal unrounded, long expected)
    {
        var actual = ParkingPricingCalculator.ApplyRounding(unrounded, MonetaryRoundingMode.CEILING_WHOLE_VND);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(1499.0, 1000L)]
    [InlineData(1500.0, 2000L)]
    [InlineData(1501.0, 2000L)]
    [InlineData(2500.0, 3000L)]
    public void TC_PRICE_09_RoundingMode_HALF_UP_THOUSAND(decimal unrounded, long expected)
    {
        var actual = ParkingPricingCalculator.ApplyRounding(unrounded, MonetaryRoundingMode.HALF_UP_THOUSAND);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(1000.0, 1000L)]
    [InlineData(1001.0, 2000L)]
    [InlineData(1999.0, 2000L)]
    public void TC_PRICE_10_RoundingMode_CEILING_THOUSAND(decimal unrounded, long expected)
    {
        var actual = ParkingPricingCalculator.ApplyRounding(unrounded, MonetaryRoundingMode.CEILING_THOUSAND);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TC_PRICE_11_ZeroMinutesParked_EntryEqualsExit_ShouldBeZeroFee()
    {
        var entry = new DateTimeOffset(2026, 10, 12, 10, 0, 0, VietnamOffset);
        var exit = new DateTimeOffset(2026, 10, 12, 10, 0, 0, VietnamOffset);
        var policy = CreateStandardPolicy(graceMinutes: 15);
        var request = CreateRequest(entry, exit, policy);

        var result = _calculator.Calculate(request);

        Assert.True(result.IsFreeStay);
        Assert.Equal(0L, result.FinalAmountVnd);
        Assert.Equal(0m, result.TotalParkedMinutes);
        Assert.Equal(0, result.InvoiceRoundedMinutes);
        Assert.Empty(result.Segments);
    }

    [Fact]
    public void TC_PRICE_12_InvalidTimeRange_ThrowsPricingDomainException()
    {
        var entry = new DateTimeOffset(2026, 10, 12, 10, 0, 0, VietnamOffset);
        var exit = new DateTimeOffset(2026, 10, 12, 9, 0, 0, VietnamOffset);
        var policy = CreateStandardPolicy();
        var request = CreateRequest(entry, exit, policy);

        var ex = Assert.Throws<PricingDomainException>(() =>
            _calculator.Calculate(request));

        Assert.Equal("INVALID_TIME_RANGE", ex.Code);
        Assert.Equal("exitTime", ex.Field);
    }

    [Fact]
    public void TC_PRICE_13_ToSnapshotJson_ContainsRequiredFieldsAndExcludesUnintendedData()
    {
        var entry = new DateTimeOffset(2026, 10, 12, 16, 30, 0, VietnamOffset);
        var exit = new DateTimeOffset(2026, 10, 12, 19, 0, 0, VietnamOffset);
        var policy = CreateStandardPolicy(graceMinutes: 15);
        var request = CreateRequest(entry, exit, policy);

        var result = _calculator.Calculate(request);
        var json = result.ToSnapshotJson();

        Assert.NotNull(json);

        // Verify JSON parseable and conforms to contract
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("currency", out var currencyProp) && currencyProp.GetString() == "VND");
        Assert.True(root.TryGetProperty("localEntryTime", out _));
        Assert.True(root.TryGetProperty("localExitTime", out _));
        Assert.True(root.TryGetProperty("timeZoneId", out var tzProp) && tzProp.GetString() == "Asia/Ho_Chi_Minh");
        Assert.True(root.TryGetProperty("totalParkedMinutes", out var totalMin) && totalMin.GetDecimal() == 150m);
        Assert.True(root.TryGetProperty("invoiceRoundedMinutes", out var invMin) && invMin.GetInt32() == 150);
        Assert.True(root.TryGetProperty("billedMinutes", out var billedMin) && billedMin.GetDecimal() == 150m);
        Assert.True(root.TryGetProperty("freeParkingGraceMinutes", out var grace) && grace.GetInt32() == 15);
        Assert.True(root.TryGetProperty("isFreeStay", out var free) && !free.GetBoolean());
        Assert.True(root.TryGetProperty("unroundedTotalVnd", out var unrounded) && unrounded.GetDecimal() == 60000m);
        Assert.True(root.TryGetProperty("finalAmountVnd", out var finalAmount) && finalAmount.GetInt64() == 60000L);
        Assert.True(root.TryGetProperty("roundingMode", out var roundMode) && roundMode.GetString() == "HALF_UP_WHOLE_VND");
        Assert.True(root.TryGetProperty("appliedTariffId", out var tariffId) && tariffId.GetGuid() == policy.TariffId);
        Assert.True(root.TryGetProperty("appliedTariffName", out var tariffName) && tariffName.GetString() == policy.TariffName);
        Assert.True(root.TryGetProperty("appliedPolicyVersion", out var policyVer) && policyVer.GetInt32() == 1);
        Assert.True(root.TryGetProperty("segments", out var segs) && segs.GetArrayLength() == 2);

        // Verify first segment
        var firstSeg = segs[0];
        Assert.True(firstSeg.TryGetProperty("segmentIndex", out var segIdx) && segIdx.GetInt32() == 1);
        Assert.True(firstSeg.TryGetProperty("localDate", out _));
        Assert.True(firstSeg.TryGetProperty("segmentStart", out _));
        Assert.True(firstSeg.TryGetProperty("segmentEnd", out _));
        Assert.True(firstSeg.TryGetProperty("blockCode", out var code) && code.GetString() == "DAY_SHIFT");
        Assert.True(firstSeg.TryGetProperty("overlapMinutes", out var overMin) && overMin.GetDecimal() == 90m);
        Assert.True(firstSeg.TryGetProperty("ratePerHourVnd", out var rate) && rate.GetInt64() == 20000L);
        Assert.True(firstSeg.TryGetProperty("rawChargeVnd", out var raw) && raw.GetDecimal() == 30000m);

        // Ensure no redundant nested "pricingSnapshotJson" property exists in snapshot
        Assert.False(root.TryGetProperty("pricingSnapshotJson", out _));
    }

    [Fact]
    public void TC_PRICE_14_VehicleTypeMismatch_ThrowsPricingDomainException()
    {
        var entry = new DateTimeOffset(2026, 10, 12, 10, 0, 0, VietnamOffset);
        var exit = new DateTimeOffset(2026, 10, 12, 11, 0, 0, VietnamOffset);
        var policy = CreateStandardPolicy(vehicleType: PricingVehicleType.MOTORCYCLE);
        var request = CreateRequest(entry, exit, policy, vehicleType: PricingVehicleType.CAR);

        var ex = Assert.Throws<PricingDomainException>(() =>
            _calculator.Calculate(request));

        Assert.Equal("VEHICLE_TYPE_MISMATCH", ex.Code);
        Assert.Equal("vehicleType", ex.Field);
    }
}
