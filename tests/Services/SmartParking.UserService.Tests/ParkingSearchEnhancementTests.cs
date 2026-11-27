using System.Text.Json;

namespace SmartParking.UserService.Tests;

public sealed class ParkingSearchEnhancementTests
{
    private const string Search = "/api/parking-search?lat=10.123456&lon=106&vehicleType=CAR";

    [Fact]
    public async Task KeywordMatchesPublishedNamesBeforeOwnerReads()
    {
        await using var host = await SearchTestHost.Start(capacityMode: "known");
        host.Owners.FarName = "Bãi đỗ Ánh";
        host.Owners.NearName = "Nhà xe";
        var rows = (await host.Data(Search + "&keyword=%20BAI%20DO%20anh%20")).GetProperty("items");
        Assert.Equal(ParkingSearchHttpTests.Far, Assert.Single(rows.EnumerateArray()).GetProperty("siteId").GetGuid());
        Assert.Equal(1, host.Owners.CandidateCalls);
        Assert.Equal(1, host.Owners.PriceCalls);
        var empty = (await host.Data(Search + "&keyword=%20%20")).GetProperty("items");
        Assert.Equal(2, empty.GetArrayLength());
        Assert.Empty((await host.Data(Search + "&keyword=unmatched")).GetProperty("items").EnumerateArray());
        Assert.Equal(2, host.Owners.PriceCalls);
        await host.Error(Search + "&keyword=" + new string('x', 101), 400);
        await host.Error(Search + "&sort=popular", 400);
    }

    [Fact]
    public async Task PriceSortUsesKnownZeroThenUnknownAndBindsFilters()
    {
        await using var host = await SearchTestHost.Start(capacityMode: "known");
        var path = Search + "&sort=price&limit=1";
        var first = (await host.Data(path)).GetProperty("items")[0];
        Assert.Equal(ParkingSearchHttpTests.Near, first.GetProperty("siteId").GetGuid());
        var cursor = Uri.EscapeDataString((await host.Data(path)).GetProperty("nextCursor").GetString()!);
        var next = path + "&cursor=" + cursor;
        Assert.Equal(ParkingSearchHttpTests.Far, (await host.Data(next)).GetProperty("items")[0].GetProperty("siteId").GetGuid());
        await host.Error(next + "&keyword=changed", 400);
        await host.Error(next.Replace("sort=price", "sort=availability"), 400);
        host.Owners.FarPrice = 0;
        await host.Error(next, 409);
        var newFirst = (await host.Data(path)).GetProperty("items")[0];
        Assert.Equal(0, newFirst.GetProperty("price").GetProperty("amountVnd").GetInt64());
    }

    [Fact]
    public async Task AvailabilitySortUsesKnownCountsAndIgnoresUnrelatedPriceChanges()
    {
        await using var host = await SearchTestHost.Start(capacityMode: "known");
        var path = Search + "&sort=availability&limit=1";
        var page = await host.Data(path);
        Assert.Equal(ParkingSearchHttpTests.Far, page.GetProperty("items")[0].GetProperty("siteId").GetGuid());
        var cursor = Uri.EscapeDataString(page.GetProperty("nextCursor").GetString()!);
        host.Owners.NearPrice = 0;
        Assert.Equal(ParkingSearchHttpTests.Near, (await host.Data(path + "&cursor=" + cursor))
            .GetProperty("items")[0].GetProperty("siteId").GetGuid());
        host.CapacitySource!.CountOverride = 0;
        await host.Error(path + "&cursor=" + cursor, 409);
    }

    [Fact]
    public async Task PriceSortIgnoresAvailabilityChangesAndIntervalUsesTotalEstimate()
    {
        await using var host = await SearchTestHost.Start(capacityMode: "known");
        var path = Search + "&sort=price&limit=1";
        var page = await host.Data(path);
        var cursor = Uri.EscapeDataString(page.GetProperty("nextCursor").GetString()!);
        host.CapacitySource!.CountOverride = 0;
        Assert.Equal(ParkingSearchHttpTests.Far, (await host.Data(path + "&cursor=" + cursor))
            .GetProperty("items")[0].GetProperty("siteId").GetGuid());
        var interval = Search + "&sort=price&startAt=2031-01-01T01:00:00Z&endAt=2031-01-01T02:00:00Z";
        Assert.Equal("estimate", (await host.Data(interval)).GetProperty("items")[0]
            .GetProperty("price").GetProperty("kind").GetString());
    }

    [Fact]
    public async Task EqualRankingValuesUseDistanceThenSiteId()
    {
        await using var host = await SearchTestHost.Start(capacityMode: "known");
        host.Owners.FarPrice = host.Owners.NearPrice;
        host.Owners.FarLatitude = 10.123456m;
        var prices = (await host.Data(Search + "&sort=price")).GetProperty("items");
        Assert.Equal(ParkingSearchHttpTests.Far, prices[0].GetProperty("siteId").GetGuid());
        host.CapacitySource!.CountOverride = 0;
        var availability = (await host.Data(Search + "&sort=availability")).GetProperty("items");
        Assert.Equal(ParkingSearchHttpTests.Far, availability[0].GetProperty("siteId").GetGuid());
    }

    [Fact]
    public async Task KeywordCombinesWithRadiusAndMotorcycleCategory()
    {
        await using var host = await SearchTestHost.Start(capacityMode: "known");
        host.Owners.FarName = "Bãi đỗ Ánh";
        host.Owners.NearName = "Nhà xe";
        Assert.Empty((await host.Data(Search + "&keyword=bai%20do%20anh&radiusKm=0.1"))
            .GetProperty("items").EnumerateArray());
        var motorcycle = Search.Replace("vehicleType=CAR", "vehicleType=MOTORCYCLE");
        var rows = (await host.Data(motorcycle + "&keyword=bai%20do%20anh")).GetProperty("items");
        Assert.Equal(ParkingSearchHttpTests.Far, Assert.Single(rows.EnumerateArray()).GetProperty("siteId").GetGuid());
        Assert.Equal(JsonValueKind.Null, rows[0].GetProperty("availability").GetProperty("availableCount").ValueKind);
    }
}
