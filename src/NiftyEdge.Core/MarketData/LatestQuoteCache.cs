using System.Collections.Concurrent;

namespace NiftyEdge.Core.MarketData;

/// <summary>
/// Holds the most recent LTP the polling service has seen for each instrument, so a page render can
/// show a live price without issuing its own provider call. Candles only land at 15-minute boundaries,
/// so reading the last candle's close instead leaves the first paint up to a full bar behind the market.
/// </summary>
public interface ILatestQuoteCache
{
    void Store(int instrumentId, LtpQuote quote);

    LtpQuote? Get(int instrumentId);
}

public class LatestQuoteCache : ILatestQuoteCache
{
    private readonly ConcurrentDictionary<int, LtpQuote> _quotes = new();

    public void Store(int instrumentId, LtpQuote quote) => _quotes[instrumentId] = quote;

    public LtpQuote? Get(int instrumentId) => _quotes.TryGetValue(instrumentId, out var quote) ? quote : null;
}
