using NiftyEdge.Core.Models;

namespace NiftyEdge.CryptoTrading.Filters;

public sealed class CryptoLiquidityFilter
{
    public TradeSignal? Apply(TradeSignal signal, IReadOnlyList<Candle> candles1h)
    {
        if (candles1h.Count < 21)
        {
            return null;
        }

        var avg = candles1h.Skip(candles1h.Count - 21).Take(20).Average(c => (decimal)c.Volume);
        var last = candles1h[^1];
        return last.Volume >= avg * 0.2m ? signal : null;
    }
}
