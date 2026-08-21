using NiftyEdge.Core.Models;

namespace NiftyEdge.CryptoTrading.Exchanges;

/// <summary>
/// Binance returns the still-forming candle as the last element of a klines response. Persisting it
/// would feed strategies a partial bar that no backtest ever saw, so REST polling keeps closed bars only.
/// </summary>
public static class ClosedCandleSelector
{
    public static TimeSpan Duration(TimeFrame timeFrame) => timeFrame switch
    {
        TimeFrame.FifteenMinute => TimeSpan.FromMinutes(15),
        TimeFrame.OneHour => TimeSpan.FromHours(1),
        TimeFrame.FourHour => TimeSpan.FromHours(4),
        _ => throw new ArgumentOutOfRangeException(nameof(timeFrame), timeFrame, "Unsupported crypto timeframe.")
    };

    /// <summary>Open time of the newest candle that has finished as of <paramref name="utcNow"/>.</summary>
    public static DateTime MostRecentClosedOpenTime(TimeFrame timeFrame, DateTime utcNow)
    {
        var duration = Duration(timeFrame);
        var flooredTicks = utcNow.Ticks - (utcNow.Ticks % duration.Ticks);
        return new DateTime(flooredTicks, DateTimeKind.Utc) - duration;
    }

    public static IReadOnlyList<Candle> SelectClosed(
        IReadOnlyList<Candle> candles,
        TimeFrame timeFrame,
        DateTime utcNow,
        int instrumentId = 0)
    {
        var duration = Duration(timeFrame);
        var closed = new List<Candle>(candles.Count);

        foreach (var candle in candles)
        {
            if (candle.CandleTime + duration > utcNow)
            {
                continue;
            }

            candle.InstrumentId = instrumentId;
            candle.TimeFrame = timeFrame;
            closed.Add(candle);
        }

        return closed;
    }
}
