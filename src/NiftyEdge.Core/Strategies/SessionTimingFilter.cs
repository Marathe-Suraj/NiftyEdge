using NiftyEdge.Core.Models;
using NiftyEdge.Core.Scheduling;

namespace NiftyEdge.Core.Strategies;

/// <summary>
/// Vetoes signals generated in the last stretch of the trading session. Backtesting NiftyEdge's
/// strategy set across ~3 years of data showed win rate falling off sharply for entries generated
/// from 14:00 IST onward (well below this system's breakeven win rate for a 1.5R target), versus a
/// solidly positive edge for entries generated earlier in the session (see BACKTEST_REPORT.md). Likely
/// driven by thinning liquidity/range as the session winds down, leaving less room for a fresh entry to
/// work before the close forces an exit. Treat this as provisional: re-validate periodically in case the
/// intraday pattern shifts.
/// </summary>
public class SessionTimingFilter
{
    public static readonly TimeSpan CutoffIst = new(14, 0, 0);

    /// <summary>Returns the signal unchanged, or null if it was generated at/after the IST cutoff.</summary>
    public TradeSignal? Apply(TradeSignal signal)
    {
        var istTime = MarketHoursCalculator.ToIst(signal.GeneratedAt);
        return istTime.TimeOfDay >= CutoffIst ? null : signal;
    }
}
