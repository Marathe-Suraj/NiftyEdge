// Shared across every page: a single SignalR connection that pushes live price ticks and
// signal events from the server. Page-specific scripts (e.g. dashboard.js) attach their own
// handlers to `window.niftyEdgeConnection` rather than opening a second connection.
(function () {
    "use strict";

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/signal")
        .withAutomaticReconnect()
        .build();

    connection.start().catch(function () { });

    window.niftyEdgeConnection = connection;
})();
