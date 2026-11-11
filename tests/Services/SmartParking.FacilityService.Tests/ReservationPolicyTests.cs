using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.FacilityService.Tests;

public sealed class ReservationPolicyTests
{
    private static ReservationPolicyRevision Platform()
    {
        var values = ReservationPolicyResolver.RequiredKeys.ToDictionary(key => key, _ => 1);
        values["RESERVATION_MIN_DURATION"] = 20;
        values["RESERVATION_MAX_DURATION"] = 120;
        values["MAX_ADVANCE_BOOKING"] = 1440;
        values["ARRIVAL_LATE_TOLERANCE"] = 15;
        return new(Guid.NewGuid(), 7, null, null, values,
            new Dictionary<string, PolicyBound> { ["PAYMENT_HOLD_DURATION"] = new(1, 10) }, null, "Asia/Ho_Chi_Minh");
    }
    private static ReservationPolicyRevision Site(bool? late, Dictionary<string, int>? values = null) =>
        new(Guid.NewGuid(), 3, Guid.NewGuid(), Guid.NewGuid(), values ?? [], new Dictionary<string, PolicyBound>(), late, null);

    [Fact]
    public void MissingPlatformOrSiteChoiceDoesNotFallBackToMemoryDefaults()
    {
        Assert.Throws<ReservationPolicyUnavailableException>(() => ReservationPolicyResolver.Resolve(null, Site(true)));
        Assert.Throws<ReservationPolicyUnavailableException>(() => ReservationPolicyResolver.Resolve(Platform(), null));
        Assert.Throws<ReservationPolicyUnavailableException>(() => ReservationPolicyResolver.Resolve(Platform(), Site(null)));
    }

    [Fact]
    public void EffectiveSnapshotRetainsRevisionAndExplicitDisabledChoice()
    {
        var platform = Platform(); var site = Site(false, new() { ["PAYMENT_HOLD_DURATION"] = 3 });
        var snapshot = ReservationPolicyResolver.Resolve(platform, site);
        Assert.False(snapshot.ArrivalLateToleranceEnabled);
        Assert.Equal(platform.Id, snapshot.Platform.Id);
        Assert.Equal(7, snapshot.Platform.Version);
        Assert.Equal(3, snapshot.Values["PAYMENT_HOLD_DURATION"]);
        var start = DateTimeOffset.UtcNow.AddHours(1);
        Assert.False(snapshot.AllowsWindow(new(start, start.AddMinutes(15)), DateTimeOffset.UtcNow));
        Assert.True(snapshot.AllowsWindow(new(start, start.AddMinutes(20)), DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData("PAYMENT_HOLD_DURATION", 11)]
    [InlineData("ALLOCATION_LEAD_TIME", 30)]
    [InlineData("DAILY_CANCELLATION_LIMIT", 20)]
    public void OutOfBoundsUnapprovedAndAccountRestrictionOverridesAreRejected(string key, int value) =>
        Assert.Throws<ReservationPolicyUnavailableException>(() => ReservationPolicyResolver.Resolve(Platform(), Site(true, new() { [key] = value })));
}
