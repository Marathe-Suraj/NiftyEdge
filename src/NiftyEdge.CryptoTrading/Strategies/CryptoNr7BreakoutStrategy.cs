using NiftyEdge.Core.Models;

namespace NiftyEdge.CryptoTrading.Strategies;

public sealed class CryptoNr7BreakoutStrategy : ICryptoStrategy
{
    private const int Lookback = 7;

    public string Name => "NR7 Breakout";

    public TradeSignal? Evaluate(CryptoStrategyContext context)
    {
        var ordered = context.Candles1h.OrderBy(c => c.CandleTime).ToList();
        if (ordered.Count < Lookback + 2)
        {
            return null;
        }

        var i = ordered.Count - 1;
        var current = ordered[i];
        var nr7Index = -1;
        for (var index = i - 1; index >= Math.Max(Lookback - 1, i - 5); index--)
        {
            if (IsNr7(ordered, index))
            {
                nr7Index = index;
                break;
            }
        }

        if (nr7Index < 0)
        {
            return null;
        }

        var nr7 = ordered[nr7Index];
        for (var index = nr7Index + 1; index < i; index++)
        {
            if (ordered[index].Close > nr7.High || ordered[index].Close < nr7.Low)
            {
                return null;
            }
        }

        if (current.Close > nr7.High)
        {
            return Build(context.Instrument, current, TradeDirection.Long, current.Close, nr7.Low);
        }

        if (current.Close < nr7.Low)
        {
            return Build(context.Instrument, current, TradeDirection.Short, current.Close, nr7.High);
        }

        return null;
    }

    private static bool IsNr7(IReadOnlyList<Candle> candles, int index)
    {
        var range = candles[index].Range;
        for (var j = index - Lookback + 1; j < index; j++)
        {
            if (candles[j].Range <= range)
            {
                return false;
            }
        }

        return true;
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
            ConfidenceScore = 58,
            Rationale = $"{direction} NR7 breakout on 1h. Past performance does not guarantee future results.",
            Status = SignalStatus.Open,
            GeneratedAt = current.CandleTime
        };
    }
}
