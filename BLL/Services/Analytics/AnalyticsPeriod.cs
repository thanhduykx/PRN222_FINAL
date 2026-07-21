namespace PRN222_FINAL.BLL.Services.Analytics;

public sealed record AnalyticsDateRange(DateTimeOffset FromUtc, DateTimeOffset ToUtc);

public static class AnalyticsPeriod
{
    public static AnalyticsDateRange ForCalendarMonth(int year, int month, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        if (year is < 1 or > 9999)
        {
            throw new ArgumentOutOfRangeException(nameof(year));
        }

        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month));
        }

        var fromLocalDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var nextMonthLocalDate = fromLocalDate.AddMonths(1);
        var fromLocal = new DateTimeOffset(fromLocalDate, timeZone.GetUtcOffset(fromLocalDate));
        var nextMonthLocal = new DateTimeOffset(nextMonthLocalDate, timeZone.GetUtcOffset(nextMonthLocalDate));
        return new AnalyticsDateRange(
            fromLocal.ToUniversalTime(),
            nextMonthLocal.ToUniversalTime().AddTicks(-1));
    }
}
