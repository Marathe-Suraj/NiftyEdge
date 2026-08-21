using NiftyEdge.Core.Models;

namespace NiftyEdge.CryptoTrading.Filters;

public sealed class CryptoPromotionFilter
{
    public TradeSignal? Apply(
        TradeSignal signal,
        bool alertOnlyPromoted,
        IReadOnlyCollection<string> promotedStrategyNames)
    {
        if (!alertOnlyPromoted)
        {
            return signal;
        }

        if (promotedStrategyNames.Count == 0)
        {
            return null;
        }

        return promotedStrategyNames.Contains(signal.StrategyName, StringComparer.OrdinalIgnoreCase)
            ? signal
            : null;
    }
}
