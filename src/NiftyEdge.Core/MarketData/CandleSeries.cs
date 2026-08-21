using NiftyEdge.Core.Models;

namespace NiftyEdge.Core.MarketData;

/// <summary>Pure helpers for shaping a fetched candle series before strategies see it.</summary>
public static class CandleSeries
{
    /// <summary>
    /// Returns only the bars that have finished forming as of <paramref name="asOfUtc"/>.
    /// Candle feeds (Yahoo's chart API included) return the currently-forming bar as the final
    /// element. Strategies trigger off the last bar's completed OHLC, so evaluating that partial
    /// bar means momentum/range/rejection conditions are measured against a few seconds of trade
    /// and effectively never fire.
    /// </summary>
    public static IReadOnlyList<Candle> CompletedAsOf(IReadOnlyList<Candle> candles, DateTime asOfUtc)
    {
        return candles
            .Where(c => asOfUtc >= c.CandleTime.AddMinutes((int)c.TimeFrame))
            .ToList();
    }
}
