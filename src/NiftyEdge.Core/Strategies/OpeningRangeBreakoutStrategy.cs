using NiftyEdge.Core.Models;

namespace NiftyEdge.Core.Strategies;

/// <summary>
/// Opening Range Breakout: the first candle of the session defines a range. A later candle that
/// closes beyond that range, with above-average range size (i.e. real momentum, not a weak poke),
/// triggers a breakout signal in the direction of the break.
/// </summary>
public class OpeningRangeBreakoutStrategy : IPriceActionStrategy
{
    private const decimal BreakoutRangeMultiplier = 1.1m;
    private const int BaseConfidence = 60;

    public string Name => "Opening Range Breakout";

    public TradeSignal? Evaluate(Instrument instrument, IReadOnlyList<Candle> candles, OptionChainSnapshot? optionChain)
    {
        if (candles.Count < 2)
        {
            return null;
        }

        var todaysCandles = candles
            .Where(c => c.CandleTime.Date == candles[^1].CandleTime.Date)
            .OrderBy(c => c.CandleTime)
            .ToList();

        if (todaysCandles.Count < 2)
        {
            return null;
        }

        var openingRange = todaysCandles[0];
        var current = todaysCandles[^1];

        if (ReferenceEquals(current, openingRange))
        {
            return null;
        }

        var priorCandles = todaysCandles.Take(todaysCandles.Count - 1).ToList();
        var averageRange = priorCandles.Average(c => c.Range);
        if (averageRange <= 0)
        {
            return null;
        }

        var hasMomentum = current.Range >= averageRange * BreakoutRangeMultiplier;

        if (current.IsBullish && current.Close > openingRange.High && hasMomentum)
        {
            return BuildSignal(instrument, candles, TradeDirection.Long, current.Close, openingRange.Low,
                $"Bullish breakout: close {current.Close:N2} above opening range high {openingRange.High:N2}, " +
                $"confirmed by a wide-range bullish candle ({current.Range:N2} vs avg {averageRange:N2}).");
        }

        if (current.IsBearish && current.Close < openingRange.Low && hasMomentum)
        {
            return BuildSignal(instrument, candles, TradeDirection.Short, current.Close, openingRange.High,
                $"Bearish breakdown: close {current.Close:N2} below opening range low {openingRange.Low:N2}, " +
                $"confirmed by a wide-range bearish candle ({current.Range:N2} vs avg {averageRange:N2}).");
        }

        return null;
    }

    private TradeSignal? BuildSignal(Instrument instrument, IReadOnlyList<Candle> candles, TradeDirection direction,
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

        var latest = candles[^1];

        return new TradeSignal
        {
            InstrumentId = instrument.InstrumentId,
            InstrumentSymbol = instrument.Symbol,
            TimeFrame = latest.TimeFrame,
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
            GeneratedAt = latest.CandleTime
        };
    }
}
