using NiftyEdge.Core.Models;

namespace NiftyEdge.Core.Strategies;

/// <summary>
/// A deterministic, rules-based price-action strategy. Every implementation must be explainable:
/// if it produces a signal, the signal's Rationale must describe exactly why it fired.
/// </summary>
public interface IPriceActionStrategy
{
    string Name { get; }

    /// <summary>
    /// Evaluates the most recent closed candle in <paramref name="candles"/> (ordered oldest-first)
    /// and returns a <see cref="TradeSignal"/> if the strategy's setup conditions are met, otherwise null.
    /// </summary>
    TradeSignal? Evaluate(Instrument instrument, IReadOnlyList<Candle> candles, OptionChainSnapshot? optionChain);
}
