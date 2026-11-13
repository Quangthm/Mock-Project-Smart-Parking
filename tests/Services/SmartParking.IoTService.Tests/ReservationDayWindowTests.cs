using SmartParking.IoTService.Application.Assistant.Chat;

namespace SmartParking.IoTService.Tests;

public sealed class ReservationDayWindowTests
{
    [Theory]
    [InlineData("2031-12-31T16:59:59Z", "2032-01-01", "2031-12-31T17:00:00Z", "2032-01-01T17:00:00Z")]
    [InlineData("2031-12-31T17:00:00Z", "2032-01-02", "2032-01-01T17:00:00Z", "2032-01-02T17:00:00Z")]
    [InlineData("2032-02-28T12:00:00Z", "2032-02-29", "2032-02-28T17:00:00Z", "2032-02-29T17:00:00Z")]
    public void TomorrowUsesInjectedClockAndExplicitVietnamZone(string now, string date, string from, string to)
    {
        var clock = new AuthenticatedChatTests.Clock { Now = DateTimeOffset.Parse(now) };
        var window = ReservationDayWindow.Tomorrow(clock, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"));
        Assert.Equal(DateOnly.Parse(date), window.LocalDate);
        Assert.Equal(DateTimeOffset.Parse(from), window.From);
        Assert.Equal(DateTimeOffset.Parse(to), window.To);
        Assert.Equal(TimeSpan.Zero, window.From.Offset);
        Assert.Equal(TimeSpan.Zero, window.To.Offset);
    }

    [Fact]
    public void OverlapIncludesCrossMidnightButExcludesTouchingBoundariesAndInvalidIntervals()
    {
        var window = ReservationDayWindow.ForDate(new(2032, 1, 1), TimeZoneInfo.Utc);
        Assert.True(window.Overlaps(window.From.AddHours(-1), window.From.AddHours(1)));
        Assert.True(window.Overlaps(window.From.AddDays(-1), window.To.AddDays(1)));
        Assert.True(window.Overlaps(window.From, window.To));
        Assert.False(window.Overlaps(window.From.AddHours(-1), window.From));
        Assert.False(window.Overlaps(window.To, window.To.AddHours(1)));
        Assert.False(window.Overlaps(window.From, window.From));
        Assert.False(window.Overlaps(window.To, window.From));
    }

    [Theory]
    [InlineData(2032, 3, 14, 23)]
    [InlineData(2032, 11, 7, 25)]
    public void LocalCalendarDayIsNotAlwaysTwentyFourUtcHours(int year, int month, int day, int hours)
    {
        var window = ReservationDayWindow.ForDate(new(year, month, day), TimeZoneInfo.FindSystemTimeZoneById("America/New_York"));
        Assert.Equal(TimeSpan.FromHours(hours), window.To - window.From);
    }
}
