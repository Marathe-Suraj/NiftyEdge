namespace NiftyEdge.Core.Scheduling;

/// <summary>
/// Converts a pair of IST calendar dates into a half-open UTC window, so filtering by "trading day"
/// matches what the user sees while the stored timestamps stay UTC.
/// </summary>
public readonly struct IstDateRange
{
    private IstDateRange(DateTime? fromUtc, DateTime? toUtcExclusive, bool isValid)
    {
        FromUtc = fromUtc;
        ToUtcExclusive = toUtcExclusive;
        IsValid = isValid;
    }

    public DateTime? FromUtc { get; }

    public DateTime? ToUtcExclusive { get; }

    public bool IsValid { get; }

    public static IstDateRange FromIstDates(DateOnly? fromDate, DateOnly? toDate)
    {
        if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
        {
            return new IstDateRange(null, null, isValid: false);
        }

        DateTime? fromUtc = fromDate.HasValue ? ToUtc(fromDate.Value) : null;
        DateTime? toUtcExclusive = toDate.HasValue ? ToUtc(toDate.Value.AddDays(1)) : null;

        return new IstDateRange(fromUtc, toUtcExclusive, isValid: true);
    }

    private static DateTime ToUtc(DateOnly istDate)
    {
        var istMidnight = DateTime.SpecifyKind(istDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(istMidnight, MarketHoursCalculator.Ist);
    }
}
