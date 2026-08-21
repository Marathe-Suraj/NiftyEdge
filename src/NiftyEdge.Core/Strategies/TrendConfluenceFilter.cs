using NiftyEdge.Core.Indicators;
using NiftyEdge.Core.Models;

namespace NiftyEdge.Core.Strategies;

/// <summary>
/// Cross-checks a 15-minute signal against the concurrent 1-hour trend. Backtesting NiftyEdge's own
/// strategy set (all reversal/pullback-style setups) across ~5,200 trades showed a consistent,
/// non-obvious pattern that held across two independent chronological halves of the data:
/// <list type="bullet">
/// <item>Signals whose direction AGREES with the 1-hour trend perform worst (net negative expectancy
/// in the more recent half) - these tend to be late reversal entries chasing an already-extended
/// hourly move.</item>
/// <item>Signals that DISAGREE with the 1-hour trend (i.e. a genuine pullback/reversal against the
/// larger trend) perform best.</item>
/// <item>Signals with no clear 1-hour trend (Neutral) perform solidly in between.</item>
/// </list>
/// This is intentionally the opposite of a typical "trade with the higher-timeframe trend" confluence
/// filter - that intuition was tested against real data here and rejected. Treat this as provisional:
/// re-validate periodically against live outcomes (see SignalHistory) in case the pattern decays.
///
/// Also applies to 1-hour signals, using a longer look-back window of the hourly series itself as the
/// "higher timeframe" proxy (an hourly EMA50 spans ~8 trading days, standing in for a genuine daily
/// trend without needing a separate daily candle feed). Validated in the backtester the same way as the
/// 15-minute case before being wired into live signal generation.
/// </summary>
public class TrendConfluenceFilter
{
    private const int DisagreementConfidenceBoost = 8;
    private const int MinimumCandlesForFifteenMinuteBiasCheck = 3;
    private const int MinimumCandlesForHourlyBiasCheck = 50;

    /// <summary>
    /// Returns the (possibly confidence-adjusted) signal, or null if the higher-timeframe trend agrees
    /// with the signal's direction (the backtested-worst case). Returns the signal unchanged if no
    /// higher-timeframe data is available, or the timeframe isn't 15-minute/1-hour (the only timeframes
    /// this filter is backtest-validated for).
    /// </summary>
    public TradeSignal? Apply(TradeSignal signal, IReadOnlyList<Candle>? higherTimeframeCandles)
    {
        if (higherTimeframeCandles is null || higherTimeframeCandles.Count == 0)
        {
            return signal;
        }

        int minimumCandles;
        string higherTimeframeLabel;
        switch (signal.TimeFrame)
        {
            case TimeFrame.FifteenMinute:
                minimumCandles = MinimumCandlesForFifteenMinuteBiasCheck;
                higherTimeframeLabel = "1-hour";
                break;
            case TimeFrame.OneHour:
                minimumCandles = MinimumCandlesForHourlyBiasCheck;
                higherTimeframeLabel = "multi-day";
                break;
            default:
                return signal;
        }

        var relevant = higherTimeframeCandles
            .Where(c => c.CandleTime <= signal.GeneratedAt)
            .OrderBy(c => c.CandleTime)
            .ToList();

        if (relevant.Count < minimumCandles)
        {
            return signal;
        }

        var bias = IndicatorMath.DetermineBias(relevant);
        var agrees = (signal.Direction == TradeDirection.Long && bias == MarketBias.Bullish)
                     || (signal.Direction == TradeDirection.Short && bias == MarketBias.Bearish);
        var disagrees = (signal.Direction == TradeDirection.Long && bias == MarketBias.Bearish)
                        || (signal.Direction == TradeDirection.Short && bias == MarketBias.Bullish);

        if (agrees)
        {
            return null; // backtested worst-performing bucket for this reversal-style strategy set - skip.
        }

        if (disagrees)
        {
            signal.ConfidenceScore = Math.Clamp(signal.ConfidenceScore + DisagreementConfidenceBoost, 0, 100);
            signal.Rationale += $" This is a genuine reversal against the {higherTimeframeLabel} {bias} trend \u2014 backtested as the strongest setup type; confidence boosted.";
        }

        return signal;
    }
}
