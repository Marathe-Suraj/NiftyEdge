namespace NiftyEdge.Core.Strategies;

public static class InstrumentRangeMinimums
{
    public static decimal MinWidth(string symbol) => symbol.ToUpperInvariant() switch
    {
        "BANKNIFTY" => 80m,
        "SENSEX" => 120m,
        "NIFTY50" => 40m,
        "FINNIFTY" => 40m,
        _ => 40m
    };

    public static decimal MinFadePoints(string symbol) => MinWidth(symbol) * 0.5m;
}
