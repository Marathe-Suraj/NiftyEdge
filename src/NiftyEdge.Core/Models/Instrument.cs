namespace NiftyEdge.Core.Models;

public class Instrument
{
    public int InstrumentId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
    public InstrumentType InstrumentType { get; set; }
    public bool IsActive { get; set; }

    /// <summary>Symbol used to query Yahoo Finance chart API, e.g. "^NSEI".</summary>
    public string YahooSymbol { get; set; } = string.Empty;

    /// <summary>Symbol used to query NSE's option-chain-indices endpoint, e.g. "NIFTY", "BANKNIFTY". Null for
    /// instruments with no NSE-listed options (e.g. BSE Sensex).</summary>
    public string NseOptionChainSymbol { get; set; } = string.Empty;

    /// <summary>Index name used to look up this instrument's quote in NSE's allIndices endpoint, e.g.
    /// "NIFTY 50", "NIFTY BANK". This is a different naming convention from <see cref="NseOptionChainSymbol"/>
    /// and NSE does not accept one in place of the other. Null for instruments with no NSE index quote
    /// (e.g. BSE Sensex).</summary>
    public string? NseIndexName { get; set; }
}
