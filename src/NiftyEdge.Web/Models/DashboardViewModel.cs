using NiftyEdge.Core.Models;

namespace NiftyEdge.Web.Models;

public class DashboardViewModel
{
    public List<InstrumentCardViewModel> Instruments { get; set; } = new();
}

public class InstrumentCardViewModel
{
    public int InstrumentId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public decimal Ltp { get; set; }
    public long LtpAsOfUnixSeconds { get; set; }
    public decimal ChangePercent { get; set; }
    public string Bias { get; set; } = "Neutral";
    public List<CandleViewModel> Candles { get; set; } = new();
    public List<TradeSignal> ActiveSignals { get; set; } = new();
    public OptionChainSummaryViewModel? OptionChainSummary { get; set; }
    public bool HasData => Candles.Count > 0;
}

public class CandleViewModel
{
    public long Time { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
}

public class OptionChainSummaryViewModel
{
    public decimal Pcr { get; set; }
    public decimal? MaxCallOiStrike { get; set; }
    public decimal? MaxPutOiStrike { get; set; }
    public decimal? MaxPainStrike { get; set; }
    public DateTime CaptureTime { get; set; }
}
