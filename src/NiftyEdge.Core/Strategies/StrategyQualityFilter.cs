using NiftyEdge.Core.Models;

namespace NiftyEdge.Core.Strategies;

/// <summary>
/// Vetoes signals from strategy/instrument combinations that backtested as net losers across ~3 years
/// of historical data (see BACKTEST_REPORT.md): "Candlestick Reversal at Key Level" on NIFTY50 and
/// SENSEX, and "Opening Range Breakout" on BANKNIFTY. All three combos showed a negative average
/// R-multiple with a large enough sample to not be noise, while the same strategies were profitable
/// on other instruments. Treat this as provisional: re-validate periodically against live outcomes
/// (see SignalHistory) in case the pattern decays or the combo recovers.
/// </summary>
public class StrategyQualityFilter
{
    private static readonly HashSet<(string StrategyName, string InstrumentSymbol)> BlockedCombos = new()
    {
        ("Candlestick Reversal at Key Level", "NIFTY50"),
        ("Candlestick Reversal at Key Level", "SENSEX"),
        ("Opening Range Breakout", "BANKNIFTY"),
    };

    /// <summary>Returns the signal unchanged, or null if it comes from a documented net-losing combo.</summary>
    public TradeSignal? Apply(TradeSignal signal) =>
        BlockedCombos.Contains((signal.StrategyName, signal.InstrumentSymbol)) ? null : signal;
}
