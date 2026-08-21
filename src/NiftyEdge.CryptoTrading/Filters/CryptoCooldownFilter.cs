using NiftyEdge.Core.Models;

namespace NiftyEdge.CryptoTrading.Filters;

public sealed class CryptoCooldownFilter
{
    private readonly Dictionary<string, DateTime> _lastSignalUtc = new(StringComparer.OrdinalIgnoreCase);

    public TradeSignal? Apply(TradeSignal signal, int cooldownHours, DateTime utcNow)
    {
        var key = $"{signal.InstrumentSymbol}:{signal.Direction}";
        if (_lastSignalUtc.TryGetValue(key, out var last) &&
            utcNow - last < TimeSpan.FromHours(cooldownHours))
        {
            return null;
        }

        _lastSignalUtc[key] = utcNow;
        return signal;
    }
}
