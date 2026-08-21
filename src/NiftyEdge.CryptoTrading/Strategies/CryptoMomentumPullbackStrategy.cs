using NiftyEdge.Core.Indicators;
using NiftyEdge.Core.Models;

namespace NiftyEdge.CryptoTrading.Strategies;

public sealed class CryptoMomentumPullbackStrategy : ICryptoStrategy
{
    public string Name => "Momentum Pullback";

    public TradeSignal? Evaluate(CryptoStrategyContext context)
    {
        var candles = context.Candles1h;
        if (candles.Count < 60)
        {
            return null;
        }

        var ema20 = IndicatorMath.Ema(candles, 20);
        var ema50 = IndicatorMath.Ema(candles, 50);
        var i = candles.Count - 1;
        var prev = candles[i - 1];
        var current = candles[i];
        if (ema20[i] is not decimal e20 || ema50[i] is not decimal e50)
        {
            return null;
        }

        var avgVol = candles.Skip(candles.Count - 21).Take(20).Average(c => (decimal)c.Volume);
        if (current.Volume <= avgVol)
        {
            return null;
        }

        if (e20 > e50)
        {
            var touched = prev.Low <= e20 * 1.002m && prev.Close >= e20 * 0.998m;
            if (touched && current.IsBullish && current.Close > prev.High)
            {
                return Build(context.Instrument, current, TradeDirection.Long, current.Close, Math.Min(prev.Low, current.Low));
            }
        }

        if (e20 < e50)
        {
            var touched = prev.High >= e20 * 0.998m && prev.Close <= e20 * 1.002m;
            if (touched && current.IsBearish && current.Close < prev.Low)
            {
                return Build(context.Instrument, current, TradeDirection.Short, current.Close, Math.Max(prev.High, current.High));
            }
        }

        return null;
    }

    private TradeSignal? Build(Instrument instrument, Candle current, TradeDirection direction, decimal entry, decimal stop)
    {
        var risk = direction == TradeDirection.Long ? entry - stop : stop - entry;
        if (risk <= 0)
        {
            return null;
        }

        return new TradeSignal
        {
            InstrumentId = instrument.InstrumentId,
            InstrumentSymbol = instrument.Symbol,
            TimeFrame = TimeFrame.OneHour,
            StrategyName = Name,
            Direction = direction,
            EntryPrice = Math.Round(entry, 4),
            StopLoss = Math.Round(stop, 4),
            Target1 = Math.Round(direction == TradeDirection.Long ? entry + risk : entry - risk, 4),
            Target2 = Math.Round(direction == TradeDirection.Long ? entry + risk * 2m : entry - risk * 2m, 4),
            RiskReward = 2.0m,
            ConfidenceScore = 57,
            Rationale = $"{direction} 1h EMA momentum pullback. Past performance does not guarantee future results.",
            Status = SignalStatus.Open,
            GeneratedAt = current.CandleTime
        };
    }
}
