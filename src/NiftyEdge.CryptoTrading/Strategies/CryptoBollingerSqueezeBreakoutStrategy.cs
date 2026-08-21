using NiftyEdge.Core.Indicators;
using NiftyEdge.Core.Models;

namespace NiftyEdge.CryptoTrading.Strategies;

public sealed class CryptoBollingerSqueezeBreakoutStrategy : ICryptoStrategy
{
    private const int BollingerPeriod = 20;
    private const decimal BollingerMultiplier = 2m;
    private const int SqueezeLookback = 20;

    public string Name => "Bollinger Squeeze Breakout";

    public TradeSignal? Evaluate(CryptoStrategyContext context)
    {
        var ordered = context.Candles1h.OrderBy(c => c.CandleTime).ToList();
        if (ordered.Count < BollingerPeriod + SqueezeLookback)
        {
            return null;
        }

        var bands = IndicatorMath.BollingerBands(ordered, BollingerPeriod, BollingerMultiplier);
        var i = ordered.Count - 1;
        if (bands.Mid[i] is not decimal mid ||
            bands.Upper[i] is not decimal upper ||
            bands.Lower[i] is not decimal lower ||
            bands.BandWidth[i] is not decimal width)
        {
            return null;
        }

        var lookback = bands.BandWidth.Skip(i - SqueezeLookback).Take(SqueezeLookback).ToList();
        var isSqueeze = lookback.All(w => w is not null) && width <= lookback.Min(w => w!.Value);
        if (!isSqueeze)
        {
            return null;
        }

        var current = ordered[i];
        if (current.Close > upper)
        {
            return Build(context.Instrument, current, TradeDirection.Long, current.Close, mid);
        }

        if (current.Close < lower)
        {
            return Build(context.Instrument, current, TradeDirection.Short, current.Close, mid);
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
            ConfidenceScore = 60,
            Rationale = $"{direction} Bollinger squeeze breakout on 1h. Past performance does not guarantee future results.",
            Status = SignalStatus.Open,
            GeneratedAt = current.CandleTime
        };
    }
}
