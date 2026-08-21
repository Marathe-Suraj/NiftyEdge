using NiftyEdge.Core.Models;

namespace NiftyEdge.CryptoTrading.Exchanges;

public interface ICryptoWebSocketClient
{
    Task ConnectAsync(
        IEnumerable<string> symbols,
        IEnumerable<TimeFrame> timeFrames,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<CryptoTicker> StreamTickersAsync(CancellationToken cancellationToken = default);

    IAsyncEnumerable<CryptoKlineUpdate> StreamKlinesAsync(CancellationToken cancellationToken = default);

    ValueTask DisposeAsync();
}
