using NiftyEdge.Core.Models;

namespace NiftyEdge.Core.MarketData;

public class LtpQuote
{
    public decimal LastTradedPrice { get; set; }
    public decimal PreviousClose { get; set; }
    public DateTime AsOf { get; set; }
    public decimal ChangePercent => PreviousClose == 0 ? 0m : Math.Round((LastTradedPrice - PreviousClose) / PreviousClose * 100m, 2);
}

/// <summary>
/// Abstraction over a source of market data, so the rest of the application (strategies, UI, scheduler)
/// never depends on where the data actually comes from. v1 ships an NSE-web provider and a Yahoo Finance
/// provider; a broker-API provider (e.g. Angel One SmartAPI) can implement this later without any other
/// code changing.
/// </summary>
public interface IMarketDataProvider
{
    string ProviderName { get; }

    Task<LtpQuote?> GetLtpAsync(Instrument instrument, CancellationToken cancellationToken = default);

    Task<OptionChainSnapshot?> GetOptionChainAsync(Instrument instrument, CancellationToken cancellationToken = default);

    /// <summary>Returns historical + latest candles for the given timeframe, oldest first.</summary>
    Task<IReadOnlyList<Candle>?> GetCandlesAsync(Instrument instrument, TimeFrame timeFrame, CancellationToken cancellationToken = default);
}
