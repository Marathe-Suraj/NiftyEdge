using NiftyEdge.Core.Models;

namespace NiftyEdge.CryptoTrading.Exchanges;

public interface ICryptoRestMarketDataClient
{
    Task<IReadOnlyList<Candle>> GetKlinesAsync(
        string symbol,
        TimeFrame timeFrame,
        DateTime? startTimeUtc = null,
        DateTime? endTimeUtc = null,
        int limit = 1500,
        CancellationToken cancellationToken = default);

    /// <summary>Last traded price per symbol, keyed case-insensitively. Symbols with no quote are omitted.</summary>
    Task<IReadOnlyDictionary<string, decimal>> GetLatestPricesAsync(
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default);
}
