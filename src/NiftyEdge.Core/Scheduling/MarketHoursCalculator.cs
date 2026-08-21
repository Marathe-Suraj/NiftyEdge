using NiftyEdge.Core.Models;

namespace NiftyEdge.Core.Scheduling;

/// <summary>Pure logic for NSE market-hours checks, kept separate from the hosted service so it's unit-testable.</summary>
public static class MarketHoursCalculator
{
    public static readonly TimeSpan MarketOpen = new(9, 15, 0);
    public static readonly TimeSpan MarketClose = new(15, 30, 0);

    private static readonly Lazy<TimeZoneInfo> IstTimeZone = new(ResolveIstTimeZone);

    public static TimeZoneInfo Ist => IstTimeZone.Value;

    public static DateTime ToIst(DateTime utcOrLocal)
    {
        var utc = utcOrLocal.Kind == DateTimeKind.Utc ? utcOrLocal : utcOrLocal.ToUniversalTime();
        return TimeZoneInfo.ConvertTimeFromUtc(utc, Ist);
    }

    /// <summary>
    /// Converts a timestamp that was persisted as UTC into IST for display. Values read back from SQL
    /// arrive as <see cref="DateTimeKind.Unspecified"/>, so they have to be pinned to UTC first:
    /// <c>ToLocalTime()</c> (and <see cref="ToIst"/>) would otherwise read them in the host's own time
    /// zone, which renders every timestamp 5:30 low on an IST box and leaves them in UTC on a UTC host.
    /// </summary>
    public static DateTime UtcToIst(DateTime storedUtc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(storedUtc, DateTimeKind.Utc), Ist);

    public static bool IsMarketOpen(DateTime nowUtc, IReadOnlySet<DateTime> holidayDates)
    {
        var istNow = ToIst(nowUtc);

        if (istNow.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            return false;
        }

        if (holidayDates.Contains(istNow.Date))
        {
            return false;
        }

        var timeOfDay = istNow.TimeOfDay;
        return timeOfDay >= MarketOpen && timeOfDay <= MarketClose;
    }

    /// <summary>
    /// Rounds <paramref name="time"/> down to the start of its <paramref name="timeFrame"/> slot.
    /// Used to decide "have I already processed this slot?" instead of testing for an exact minute:
    /// the poll timer drifts, so an exact-minute check can miss a boundary outright with no catch-up.
    /// </summary>
    public static DateTime FloorToInterval(DateTime time, TimeFrame timeFrame)
    {
        var intervalMinutes = (int)timeFrame;
        var minutesFromMidnight = (time.Hour * 60) + time.Minute;
        var slotMinutes = minutesFromMidnight - (minutesFromMidnight % intervalMinutes);

        return time.Date.AddMinutes(slotMinutes);
    }

    private static TimeZoneInfo ResolveIstTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
        }
    }
}
