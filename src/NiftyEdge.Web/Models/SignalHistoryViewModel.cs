using NiftyEdge.Core.Models;

namespace NiftyEdge.Web.Models;

public class SignalHistoryViewModel
{
    public const int DefaultPageSize = 20;

    public SignalMarketScope MarketScope { get; set; } = SignalMarketScope.Equity;
    public string PageTitle { get; set; } = "Equity Signals";
    public string PageSubtitle { get; set; } = "Browse and filter equity signals across your watchlist.";
    public string ControllerName { get; set; } = "SignalHistory";

    public int? SelectedInstrumentId { get; set; }
    public string? SelectedStrategyName { get; set; }
    public DateOnly? SelectedFromDate { get; set; }
    public DateOnly? SelectedToDate { get; set; }
    public bool HasInvalidDateRange { get; set; }
    public List<Instrument> Instruments { get; set; } = new();
    public List<TradeSignal> Signals { get; set; } = new();
    public IReadOnlyList<string> StrategyNames { get; set; } = EquityStrategyNames;

    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = DefaultPageSize;
    public int TotalCount { get; set; }
    public int OpenCount { get; set; }
    public int StopHitCount { get; set; }
    public int TargetHitCount { get; set; }

    public int TotalPages => TotalCount <= 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public int FromRow => TotalCount == 0 ? 0 : ((PageNumber - 1) * PageSize) + 1;
    public int ToRow => Math.Min(PageNumber * PageSize, TotalCount);

    public static readonly string[] EquityStrategyNames =
    {
        "Opening Range Breakout",
        "VWAP Pullback",
        "Pivot Point Reversal",
        "Candlestick Reversal at Key Level"
    };

    public static readonly string[] CryptoStrategyNames =
    {
        "Trend Pullback Confirmation",
        "Momentum Pullback",
        "Bollinger Squeeze Breakout",
        "NR7 Breakout"
    };

    public static IReadOnlyList<string> StrategiesFor(SignalMarketScope scope) =>
        scope == SignalMarketScope.Crypto ? CryptoStrategyNames : EquityStrategyNames;
}
