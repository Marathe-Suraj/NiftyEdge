using NiftyEdge.Core.Indicators;
using NiftyEdge.Core.Models;

namespace NiftyEdge.Core.Strategies;

/// <summary>
/// Detects reversal candlestick patterns (bullish/bearish engulfing, hammer, shooting star) that occur
/// at a confluence level (prior day high/low, or the daily pivot) and signals a reversal in the
/// implied direction. Patterns away from a meaningful level are ignored to reduce noise.
/// </summary>
public class CandlestickReversalAtLevelStrategy : IPriceActionStrategy
{
    private const decimal LevelProximityPercent = 0.15m;
    private const int BaseConfidence = 55;

    public string Name => "Candlestick Reversal at Key Level";

    public TradeSignal? Evaluate(Instrument instrument, IReadOnlyList<Candle> candles, OptionChainSnapshot? optionChain)
    {
        if (candles.Count < 2)
        {
            return null;
        }

        var byDate = candles.OrderBy(c => c.CandleTime).GroupBy(c => c.CandleTime.Date).ToList();
        if (byDate.Count < 2)
        {
            return null;
        }

        var priorGroup = byDate[^2];
        var priorHigh = priorGroup.Max(c => c.High);
        var priorLow = priorGroup.Min(c => c.Low);
        var priorClose = priorGroup.OrderBy(c => c.CandleTime).Last().Close;
        var pivots = PivotPointCalculator.Calculate(priorHigh, priorLow, priorClose);

        var supportLevels = new[] { priorLow, pivots.Support1 };
        var resistanceLevels = new[] { priorHigh, pivots.Resistance1 };

        var previous = candles[^2];
        var current = candles[^1];
        var pattern = CandlestickPatternDetector.Detect(previous, current);

        var isBullishPattern = pattern is CandlestickPattern.BullishEngulfing or CandlestickPattern.Hammer;
        var isBearishPattern = pattern is CandlestickPattern.BearishEngulfing or CandlestickPattern.ShootingStar;

        if (isBullishPattern && IsNearAnyLevel(current, supportLevels, out var supportLevel))
        {
            return BuildSignal(instrument, current, TradeDirection.Long, current.Close, current.Low,
                $"{DescribePattern(pattern)} formed near support level {supportLevel:N2} " +
                $"(prior day low / S1 confluence), signalling a bounce.");
        }

        if (isBearishPattern && IsNearAnyLevel(current, resistanceLevels, out var resistanceLevel))
        {
            return BuildSignal(instrument, current, TradeDirection.Short, current.Close, current.High,
                $"{DescribePattern(pattern)} formed near resistance level {resistanceLevel:N2} " +
                $"(prior day high / R1 confluence), signalling a rejection.");
        }

        return null;
    }

    private static bool IsNearAnyLevel(Candle candle, IReadOnlyList<decimal> levels, out decimal matchedLevel)
    {
        foreach (var level in levels)
        {
            var tolerance = level * (LevelProximityPercent / 100m);
            if (candle.Low - tolerance <= level && level <= candle.High + tolerance)
            {
                matchedLevel = level;
                return true;
            }
        }

        matchedLevel = 0m;
        return false;
    }

    private static string DescribePattern(CandlestickPattern pattern) => pattern switch
    {
        CandlestickPattern.BullishEngulfing => "Bullish engulfing candle",
        CandlestickPattern.BearishEngulfing => "Bearish engulfing candle",
        CandlestickPattern.Hammer => "Hammer candle",
        CandlestickPattern.ShootingStar => "Shooting star candle",
        _ => "Reversal candle"
    };

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
