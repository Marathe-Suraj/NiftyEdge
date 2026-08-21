using NiftyEdge.Core.Indicators;
using NiftyEdge.Core.Models;

namespace NiftyEdge.Core.Strategies;

/// <summary>
/// Computes classic daily pivot levels from the prior session's High/Low/Close, then signals a reversal
/// only on a genuine level rejection: the current candle must touch a strong level (S1/R1/S2/R2 - the
/// mid-pivot is intentionally ignored as the noisiest), print a rejection wick back off that level,
/// close decisively away from it, and carry a stop distance that is meaningful relative to recent
/// volatility (ATR). A mere touch-and-weak-close no longer qualifies - that produced noise-sized stops.
/// </summary>
public class PivotBreakoutReversalStrategy : IPriceActionStrategy
{
    private const int BaseConfidence = 58;
    private const int AtrPeriod = 14;

    // A real rejection leaves a wick back toward the level worth at least this share of the candle's range...
    private const decimal MinRejectionWickToRangeRatio = 0.40m;

    // ...and closes at least this share of the range beyond the level (not sitting right on it).
    private const decimal MinCloseBeyondLevelToRangeRatio = 0.25m;

    // The stop (entry candle wick) must be at least this multiple of ATR, or the trade is just noise.
    private const decimal MinStopToAtrRatio = 0.35m;

    public string Name => "Pivot Point Reversal";

    public TradeSignal? Evaluate(Instrument instrument, IReadOnlyList<Candle> candles, OptionChainSnapshot? optionChain)
    {
        var ordered = candles.OrderBy(c => c.CandleTime).ToList();
        var byDate = ordered
            .GroupBy(c => c.CandleTime.Date)
            .ToList();

        if (byDate.Count < 2)
        {
            return null;
        }

        var todayGroup = byDate[^1];
        var priorGroup = byDate[^2];

        var priorHigh = priorGroup.Max(c => c.High);
        var priorLow = priorGroup.Min(c => c.Low);
        var priorClose = priorGroup.OrderBy(c => c.CandleTime).Last().Close;

        var pivots = PivotPointCalculator.Calculate(priorHigh, priorLow, priorClose);
        var current = todayGroup.OrderBy(c => c.CandleTime).Last();

        if (current.Range <= 0)
        {
            return null;
        }

        // Strong levels only - the mid-pivot (P) is dropped as the noisiest, most-often-chopped level.
        var levels = new[]
        {
            (Name: "S2", Value: pivots.Support2),
            (Name: "S1", Value: pivots.Support1),
            (Name: "R1", Value: pivots.Resistance1),
            (Name: "R2", Value: pivots.Resistance2)
        };

        var touchedLevel = levels
            .Where(l => current.Low <= l.Value && l.Value <= current.High)
            .OrderBy(l => Math.Abs(current.Close - l.Value))
            .FirstOrDefault();

        if (touchedLevel.Value == 0)
        {
            return null;
        }

        var atrAtCurrent = IndicatorMath.Atr(ordered, AtrPeriod)[^1];

        if (current.Close > touchedLevel.Value && current.IsBullish
            && IsGenuineRejection(current, touchedLevel.Value, TradeDirection.Long, atrAtCurrent))
        {
            return BuildSignal(instrument, current, TradeDirection.Long, current.Close, current.Low,
                $"Price touched {touchedLevel.Name} pivot level ({touchedLevel.Value:N2}) and reversed higher, " +
                $"closing at {current.Close:N2} \u2014 level held as support.");
        }

        if (current.Close < touchedLevel.Value && current.IsBearish
            && IsGenuineRejection(current, touchedLevel.Value, TradeDirection.Short, atrAtCurrent))
        {
            return BuildSignal(instrument, current, TradeDirection.Short, current.Close, current.High,
                $"Price touched {touchedLevel.Name} pivot level ({touchedLevel.Value:N2}) and reversed lower, " +
                $"closing at {current.Close:N2} \u2014 level held as resistance.");
        }

        return null;
    }

    /// <summary>
    /// A touch only counts as a tradable rejection when the candle leaves a wick back toward the level,
    /// closes decisively away from it, and (when ATR is available) carries a non-trivial stop distance.
    /// </summary>
    private static bool IsGenuineRejection(Candle current, decimal level, TradeDirection direction, decimal? atr)
    {
        var range = current.Range;

        var rejectionWick = direction == TradeDirection.Long ? current.LowerWick : current.UpperWick;
        if (rejectionWick < range * MinRejectionWickToRangeRatio)
        {
            return false;
        }

        var closeBeyondLevel = direction == TradeDirection.Long ? current.Close - level : level - current.Close;
        if (closeBeyondLevel < range * MinCloseBeyondLevelToRangeRatio)
        {
            return false;
        }

        if (atr is decimal atrValue && atrValue > 0)
        {
            var risk = direction == TradeDirection.Long ? current.Close - current.Low : current.High - current.Close;
            if (risk < atrValue * MinStopToAtrRatio)
            {
                return false;
            }
        }

        return true;
    }

    private TradeSignal? BuildSignal(Instrument instrument, Candle current, TradeDirection direction,
        decimal entry, decimal stopLoss, string rationale)
    {
        var risk = direction == TradeDirection.Long ? entry - stopLoss : stopLoss - entry;
        if (risk <= 0)
        {
            return null;
        }

        var target1 = direction == TradeDirection.Long
            ? entry + (risk * TradeSignal.MinimumRiskReward)
            : entry - (risk * TradeSignal.MinimumRiskReward);

        var target2 = direction == TradeDirection.Long
            ? entry + (risk * 2.5m)
            : entry - (risk * 2.5m);

        return new TradeSignal
        {
            InstrumentId = instrument.InstrumentId,
            InstrumentSymbol = instrument.Symbol,
            TimeFrame = current.TimeFrame,
            StrategyName = Name,
            Direction = direction,
            EntryPrice = Math.Round(entry, 2),
            StopLoss = Math.Round(stopLoss, 2),
            Target1 = Math.Round(target1, 2),
            Target2 = Math.Round(target2, 2),
            RiskReward = TradeSignal.CalculateRiskReward(entry, stopLoss, target1, direction),
            ConfidenceScore = BaseConfidence,
            Rationale = rationale,
            Status = SignalStatus.Open,
            GeneratedAt = current.CandleTime
        };
    }
}
