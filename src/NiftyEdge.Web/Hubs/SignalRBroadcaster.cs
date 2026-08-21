using Microsoft.AspNetCore.SignalR;
using NiftyEdge.Core.MarketData;
using NiftyEdge.Core.Models;
using NiftyEdge.Core.Signals;

namespace NiftyEdge.Web.Hubs;

/// <summary>The only place in the app that knows about SignalR; implements the Core-defined
/// <see cref="ISignalBroadcaster"/> abstraction so NiftyEdge.Core stays framework-agnostic.</summary>
public class SignalRBroadcaster : ISignalBroadcaster
{
    private readonly IHubContext<SignalHub> _hubContext;

    public SignalRBroadcaster(IHubContext<SignalHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task BroadcastNewSignalAsync(TradeSignal signal, CancellationToken cancellationToken = default) =>
        _hubContext.Clients.All.SendAsync("newSignal", signal, cancellationToken);

    public Task BroadcastSignalUpdatedAsync(TradeSignal signal, CancellationToken cancellationToken = default) =>
        _hubContext.Clients.All.SendAsync("signalUpdated", signal, cancellationToken);

    public Task BroadcastPriceUpdateAsync(int instrumentId, LtpQuote quote, CancellationToken cancellationToken = default) =>
        _hubContext.Clients.All.SendAsync("priceUpdate", instrumentId, quote, cancellationToken);
}
