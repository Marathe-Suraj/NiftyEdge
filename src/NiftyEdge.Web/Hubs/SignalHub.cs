using Microsoft.AspNetCore.SignalR;

namespace NiftyEdge.Web.Hubs;

/// <summary>
/// Intentionally empty: the server only ever pushes to clients (new/updated signals, price ticks)
/// via <see cref="SignalRBroadcaster"/>. No client-to-server methods are needed for v1.
/// </summary>
public class SignalHub : Hub
{
}
