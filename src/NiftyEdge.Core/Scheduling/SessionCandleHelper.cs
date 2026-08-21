using NiftyEdge.Core.Models;

namespace NiftyEdge.Core.Scheduling;

public static class SessionCandleHelper
{
    public static readonly TimeSpan SquareOffIst = new(15, 15, 0);

    public static DateOnly SessionDateIst(DateTime candleTime) =>
        DateOnly.FromDateTime(ToIst(candleTime));

    public static IReadOnlyList<IGrouping<DateOnly, Candle>> GroupBySessionIst(IEnumerable<Candle> candles) =>
        candles
            .OrderBy(c => c.CandleTime)
            .GroupBy(c => SessionDateIst(c.CandleTime))
            .OrderBy(g => g.Key)
            .ToList();

    public static (decimal High, decimal Low, decimal Open, decimal Close)? GetOpeningRange(
        IReadOnlyList<Candle> candles, DateOnly sessionDateIst, int barCount)
    {
        var sessionBars = candles
            .Where(c => SessionDateIst(c.CandleTime) == sessionDateIst)
            .Where(c => ToIst(c.CandleTime).TimeOfDay >= MarketHoursCalculator.MarketOpen)
            .OrderBy(c => c.CandleTime)
            .Take(barCount)
            .ToList();

        if (sessionBars.Count < barCount)
        {
            return null;
        }

        return (
            sessionBars.Max(c => c.High),
            sessionBars.Min(c => c.Low),
            sessionBars[0].Open,
            sessionBars[^1].Close);
    }

    public static (decimal High, decimal Low, decimal Close)? GetPreviousSessionLevels(
        IReadOnlyList<Candle> candles, DateOnly currentSessionIst)
    {
        var groups = GroupBySessionIst(candles)
            .Where(g => g.Key < currentSessionIst)
            .ToList();

        if (groups.Count == 0)
        {
            return null;
        }

        var prior = groups[^1]
            .Where(c =>
            {
                var timeOfDay = ToIst(c.CandleTime).TimeOfDay;
                return timeOfDay >= MarketHoursCalculator.MarketOpen
                    && timeOfDay <= MarketHoursCalculator.MarketClose;
            })
            .ToList();

        if (prior.Count == 0)
        {
            return null;
        }

        return (prior.Max(c => c.High), prior.Min(c => c.Low), prior.OrderBy(c => c.CandleTime).Last().Close);
    }

    private static DateTime ToIst(DateTime candleTime) =>
        MarketHoursCalculator.ToIst(candleTime.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(candleTime, DateTimeKind.Utc)
            : candleTime);

    public static bool IsAtOrAfterSquareOff(DateTime candleTime)
    {
        var ist = ToIst(candleTime);
        return ist.TimeOfDay >= SquareOffIst;
    }
}
