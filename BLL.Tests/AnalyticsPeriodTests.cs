using PRN222_FINAL.BLL.Services.Analytics;
using Xunit;

namespace PRN222_FINAL.BLL.Tests;

public sealed class AnalyticsPeriodTests
{
    [Fact]
    public void ForCalendarMonth_UsesTheCompleteMonthInTheSelectedTimeZone()
    {
        var vietnamTime = TimeZoneInfo.CreateCustomTimeZone(
            "UTC+07-test",
            TimeSpan.FromHours(7),
            "UTC+07-test",
            "UTC+07-test");

        var range = AnalyticsPeriod.ForCalendarMonth(2026, 7, vietnamTime);

        Assert.Equal(new DateTimeOffset(2026, 6, 30, 17, 0, 0, TimeSpan.Zero), range.FromUtc);
        Assert.Equal(new DateTimeOffset(2026, 7, 31, 16, 59, 59, TimeSpan.Zero).AddTicks(9_999_999), range.ToUtc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void ForCalendarMonth_RejectsInvalidMonth(int month)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AnalyticsPeriod.ForCalendarMonth(2026, month, TimeZoneInfo.Utc));
    }
}
