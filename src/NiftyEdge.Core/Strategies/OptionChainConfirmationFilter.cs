using NiftyEdge.Core.Models;

namespace NiftyEdge.Core.Strategies;

/// <summary>
/// Not a standalone signal generator. Adjusts (boosts, downgrades, or vetoes) a signal already
/// produced by one of the price-action strategies, using option-chain open-interest data:
/// Put/Call ratio for overall sentiment, and the nearest large OI "wall" for a hard support/resistance check.
/// </summary>
public class OptionChainConfirmationFilter
{
    private const int ConfidenceBoost = 15;
    private const int ConfidencePenalty = 20;
    private const decimal WallProximityFraction = 0.005m; // 0.5% of underlying price

    /// <summary>
    /// Returns the (possibly confidence-adjusted) signal, or null if the option-chain data vetoes it outright
    /// (e.g. a bullish breakout aimed straight into a massive Call OI wall before Target1).
    /// </summary>
    public TradeSignal? Apply(TradeSignal signal, OptionChainSnapshot? optionChain)
    {
        if (optionChain is null || optionChain.Rows.Count == 0)
        {
            return signal;
        }

        var pcr = optionChain.PutCallRatio;
        var sentimentAgrees = signal.Direction == TradeDirection.Long ? pcr > 1.0m : pcr < 1.0m;
        var sentimentDisagrees = signal.Direction == TradeDirection.Long ? pcr < 0.7m : pcr > 1.3m;

        var wallLevel = signal.Direction == TradeDirection.Long ? optionChain.MaxCallOiStrike : optionChain.MaxPutOiStrike;
        var wallBlocksTarget = wallLevel is not null && IsWallBetweenEntryAndTarget(signal, wallLevel.Value);
        var wallTolerance = signal.EntryPrice * WallProximityFraction;
        var priceAtWallAlready = wallLevel is not null && Math.Abs(signal.EntryPrice - wallLevel.Value) <= wallTolerance;

        if (wallBlocksTarget && !priceAtWallAlready)
        {
            var wallName = signal.Direction == TradeDirection.Long ? "Call OI wall" : "Put OI wall";
            return DowngradeAndAnnotate(signal, -ConfidencePenalty,
                $" Caution: a large {wallName} sits at {wallLevel:N2} before Target1 \u2014 confidence reduced.");
        }

        if (sentimentAgrees)
        {
            return DowngradeAndAnnotate(signal, ConfidenceBoost,
                $" Option-chain PCR of {pcr:N2} supports this direction \u2014 confidence boosted.");
        }

        if (sentimentDisagrees)
        {
            return DowngradeAndAnnotate(signal, -ConfidencePenalty,
                $" Option-chain PCR of {pcr:N2} conflicts with this direction \u2014 confidence reduced.");
        }

        return signal;
    }

    private static bool IsWallBetweenEntryAndTarget(TradeSignal signal, decimal wallLevel)
    {
        return signal.Direction == TradeDirection.Long
            ? wallLevel > signal.EntryPrice && wallLevel < signal.Target1
            : wallLevel < signal.EntryPrice && wallLevel > signal.Target1;
    }

    private static TradeSignal DowngradeAndAnnotate(TradeSignal signal, int confidenceDelta, string annotation)
    {
        signal.ConfidenceScore = Math.Clamp(signal.ConfidenceScore + confidenceDelta, 0, 100);
        signal.Rationale += annotation;
        return signal;
    }
}
