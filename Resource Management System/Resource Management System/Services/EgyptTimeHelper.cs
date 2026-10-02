namespace Resource_Management_System.Services;

/// <summary>
/// All request/approval/issue timestamps are stored in the database in UTC
/// (sysutcdatetime()). Views must convert to Egypt local time before display,
/// otherwise submission times appear 2-3 hours behind the user's clock.
///
/// This mirrors the exact DST rule already implemented in the database as
/// dbo.fn_EgyptUtcToLocal / dbo.fn_EgyptLastFriday, so the app and the
/// database always agree on the current offset: Egypt has used DST since
/// 2023, from the last Friday of April to the last Friday of October
/// (UTC+3 during that window, UTC+2 the rest of the year).
/// </summary>
public static class EgyptTimeHelper
{
    /// <summary>Converts a UTC DateTime to Egypt local time for display.</summary>
    public static DateTime ToEgyptTime(this DateTime utcValue)
    {
        int year = utcValue.AddHours(2).Year;
        int offsetHours = 2;

        var dstStartUtc = LastFridayOfMonth(year, 4).AddHours(-2);
        var dstEndUtc = LastFridayOfMonth(year, 10).AddHours(-3);

        if (utcValue >= dstStartUtc && utcValue < dstEndUtc)
        {
            offsetHours = 3;
        }

        return utcValue.AddHours(offsetHours);
    }

    /// <summary>Converts a nullable UTC DateTime to Egypt local time for display.</summary>
    public static DateTime? ToEgyptTime(this DateTime? utcValue)
        => utcValue.HasValue ? utcValue.Value.ToEgyptTime() : null;

    private static DateTime LastFridayOfMonth(int year, int month)
    {
        var lastDay = new DateTime(year, month, DateTime.DaysInMonth(year, month));
        int diff = ((int)lastDay.DayOfWeek - (int)DayOfWeek.Friday + 7) % 7;
        return lastDay.AddDays(-diff);
    }
}
