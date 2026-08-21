using NiftyEdge.Core.Indicators;
using NiftyEdge.Core.Models;

namespace NiftyEdge.Core.Strategies;

/// <summary>
/// A strategy-agnostic chart-quality floor applied to every raw signal before the other filters. It
/// vetoes setups whose entry candle shows no real conviction (indecisive body), points the wrong way
/// for the signal's direction, or carries a stop that is either noise-sized or absurdly wide relative
/// to recent volatility (ATR). This does NOT re-check strategy-specific structure (e.g. pivot rejection
/// wicks) - it is the shared minimum bar that all strategies must clear. Treat the thresholds as
/// provisional and re-validate against backtests if the strategy mix changes.
/// </summary>
public class CandleQualityFilter
{
    private const int AtrPeriod = 14;
    private const decimal MinBodyToRangeRatio = 0.25m;
    private const decimal MinStopToAtrRatio = 0.25m;
    private const decimal MaxStopToAtrRatio = 2.5m;

    /// <summary>
    /// Returns the signal unchanged if it clears the quality floor, or null to veto it. Pass the same
    /// own-timeframe candle series the strategy evaluated; the entry candle is the last one at or before
    /// <see cref="TradeSignal.GeneratedAt"/>. The ATR-based checks are skipped when there is not yet
    /// enough history to compute ATR.
    /// </summary>
    public TradeSignal? Apply(TradeSignal signal, IReadOnlyList<Candle> ownTimeframeCandles)
    {
        var relevant = ownTimeframeCandles
            .Where(c => c.CandleTime <= signal.GeneratedAt)
            .OrderBy(c => c.CandleTime)
            .ToList();

        if (relevant.Count == 0)
        {
            return signal;
        }

        var entryCandle = relevant[^1];

        if (entryCandle.Range <= 0 || entryCandle.Body / entryCandle.Range < MinBodyToRangeRatio)
        {
            return null;
        }

        var directionAgrees = signal.Direction == TradeDirection.Long ? entryCandle.IsBullish : entryCandle.IsBearish;
        if (!directionAgrees)
        {
            return null;
        }

        var atr = IndicatorMath.Atr(relevant, AtrPeriod)[^1];
        if (atr is decimal atrValue && atrValue > 0)
        {
            var risk = signal.Direction == TradeDirection.Long
                ? signal.EntryPrice - signal.StopLoss
                : signal.StopLoss - signal.EntryPrice;

            if (risk < atrValue * MinStopToAtrRatio || risk > atrValue * MaxStopToAtrRatio)
            {
                return null;
            }
        }

        return signal;
    }
}
