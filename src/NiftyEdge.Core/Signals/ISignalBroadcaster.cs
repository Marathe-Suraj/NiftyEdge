using NiftyEdge.Core.MarketData;
using NiftyEdge.Core.Models;

namespace NiftyEdge.Core.Signals;

/// <summary>
/// Push abstraction so Core never depends on ASP.NET Core / SignalR directly. The Web project's
/// SignalR-hub-based implementation is registered against this interface in DI.
/// </summary>
public interface ISignalBroadcaster
{
    Task BroadcastNewSignalAsync(TradeSignal signal, CancellationToken cancellationToken = default);

    Task BroadcastSignalUpdatedAsync(TradeSignal signal, CancellationToken cancellationToken = default);

    Task BroadcastPriceUpdateAsync(int instrumentId, LtpQuote quote, CancellationToken cancellationToken = default);
}
