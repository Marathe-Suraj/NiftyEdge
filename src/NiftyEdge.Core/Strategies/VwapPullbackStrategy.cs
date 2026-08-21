using NiftyEdge.Core.Indicators;
using NiftyEdge.Core.Models;

namespace NiftyEdge.Core.Strategies;

/// <summary>
/// Trades in the direction of the session VWAP's slope, entering when price pulls back to VWAP
/// and is rejected back in the trend direction (i.e. VWAP acting as dynamic support/resistance).
/// </summary>
public class VwapPullbackStrategy : IPriceActionStrategy
{
    private const int SlopeLookback = 3;
    private const int BaseConfidence = 62;

    public string Name => "VWAP Pullback";

    public TradeSignal? Evaluate(Instrument instrument, IReadOnlyList<Candle> candles, OptionChainSnapshot? optionChain)
    {
        var todaysCandles = candles
            .Where(c => c.CandleTime.Date == candles[^1].CandleTime.Date)
            .OrderBy(c => c.CandleTime)
            .ToList();

        if (todaysCandles.Count < SlopeLookback + 1)
        {
            return null;
        }

        var vwap = IndicatorMath.Vwap(todaysCandles);
        var lastIndex = todaysCandles.Count - 1;
        var currentVwap = vwap[lastIndex];
        var priorVwap = vwap[lastIndex - SlopeLookback];

        if (currentVwap is null || priorVwap is null)
        {
            return null;
        }

        var current = todaysCandles[lastIndex];
        var slopeUp = currentVwap.Value > priorVwap.Value;
        var slopeDown = currentVwap.Value < priorVwap.Value;

        var touchedVwapFromAbove = current.Low <= currentVwap.Value && current.Close > currentVwap.Value;
        var touchedVwapFromBelow = current.High >= currentVwap.Value && current.Close < currentVwap.Value;

        if (slopeUp && touchedVwapFromAbove && current.IsBullish)
        {
            return BuildSignal(instrument, current, TradeDirection.Long, current.Close, current.Low,
                $"Uptrend (VWAP rising {priorVwap.Value:N2} \u2192 {currentVwap.Value:N2}); price pulled back to VWAP " +
                $"{currentVwap.Value:N2} and was rejected higher, closing at {current.Close:N2}.");
        }

        if (slopeDown && touchedVwapFromBelow && current.IsBearish)
        {
            return BuildSignal(instrument, current, TradeDirection.Short, current.Close, current.High,
                $"Downtrend (VWAP falling {priorVwap.Value:N2} \u2192 {currentVwap.Value:N2}); price pulled back to VWAP " +
                $"{currentVwap.Value:N2} and was rejected lower, closing at {current.Close:N2}.");
        }

        return null;
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
