using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using NiftyEdge.Core.Models;
using NiftyEdge.CryptoTrading.Exchanges;

namespace NiftyEdge.CryptoTrading.Exchanges.Binance;

/// <summary>
/// Binance drops idle/long-lived public streams routinely (and forces a disconnect every 24h), so the
/// socket is supervised: the reader channels stay open across drops and a background loop reconnects
/// with capped exponential backoff. Only disposal or cancellation completes the channels.
/// </summary>
public sealed class BinanceUsdtmFuturesPublicWebSocketClient : ICryptoWebSocketClient, IAsyncDisposable
{
    private static readonly TimeSpan MaxReconnectDelay = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan StableSessionThreshold = TimeSpan.FromSeconds(60);

    private readonly ILogger<BinanceUsdtmFuturesPublicWebSocketClient> _logger;
    private readonly Channel<CryptoTicker> _tickers = Channel.CreateUnbounded<CryptoTicker>();
    private readonly Channel<CryptoKlineUpdate> _klines = Channel.CreateUnbounded<CryptoKlineUpdate>();
    private ClientWebSocket? _socket;
    private CancellationTokenSource? _supervisorCts;
    private Task? _supervisor;
    private Uri? _streamUri;
    private int _streamCount;

    public BinanceUsdtmFuturesPublicWebSocketClient(ILogger<BinanceUsdtmFuturesPublicWebSocketClient> logger)
    {
        _logger = logger;
    }

    public Task ConnectAsync(
        IEnumerable<string> symbols,
        IEnumerable<TimeFrame> timeFrames,
        CancellationToken cancellationToken = default)
    {
        var symbolList = symbols
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim().ToLowerInvariant())
            .Where(s => !s.StartsWith("btc", StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToList();

        var tfList = timeFrames.Distinct().ToList();
        if (symbolList.Count == 0)
        {
            throw new ArgumentException("At least one non-BTC symbol is required.", nameof(symbols));
        }

        var streams = new List<string>();
        foreach (var symbol in symbolList)
        {
            streams.Add($"{symbol}@markPrice@1s");
            foreach (var tf in tfList)
            {
                streams.Add($"{symbol}@kline_{BinanceKlineParser.ToBinanceInterval(tf)}");
            }
        }

        _streamCount = streams.Count;
        _streamUri = new Uri("wss://fstream.binance.com/stream?streams=" + string.Join("/", streams));

        _supervisorCts?.Cancel();
        _supervisorCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _supervisor = Task.Run(() => SuperviseAsync(_supervisorCts.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<CryptoTicker> StreamTickersAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var ticker in _tickers.Reader.ReadAllAsync(cancellationToken))
        {
            yield return ticker;
        }
    }

    public async IAsyncEnumerable<CryptoKlineUpdate> StreamKlinesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var kline in _klines.Reader.ReadAllAsync(cancellationToken))
        {
            yield return kline;
        }
    }

    private async Task SuperviseAsync(CancellationToken cancellationToken)
    {
        var failedAttempts = 0;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var connectedAt = DateTime.UtcNow;

                try
                {
                    await OpenSocketAsync(cancellationToken);
                    connectedAt = DateTime.UtcNow;
                    _logger.LogInformation(
                        "Connected to Binance futures public websocket with {StreamCount} streams.", _streamCount);

                    await ReceiveLoopAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Binance websocket session ended unexpectedly.");
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                // A session that stayed up is treated as healthy, so a later drop reconnects promptly
                // instead of inheriting backoff from an unrelated outage.
                failedAttempts = DateTime.UtcNow - connectedAt >= StableSessionThreshold ? 0 : failedAttempts + 1;

                var delay = TimeSpan.FromSeconds(Math.Min(MaxReconnectDelay.TotalSeconds, Math.Pow(2, Math.Min(failedAttempts, 6))));
                _logger.LogInformation("Reconnecting to Binance websocket in {Delay}s.", delay.TotalSeconds);

                try
                {
                    await Task.Delay(delay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        finally
        {
            _tickers.Writer.TryComplete();
            _klines.Writer.TryComplete();
        }
    }

    private async Task OpenSocketAsync(CancellationToken cancellationToken)
    {
        var uri = _streamUri ?? throw new InvalidOperationException("ConnectAsync must be called first.");

        _socket?.Dispose();
        _socket = new ClientWebSocket();
        _socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);
        await _socket.ConnectAsync(uri, cancellationToken);
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        var socket = _socket ?? throw new InvalidOperationException("Socket is not connected.");

        while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            using var ms = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }

                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            var json = Encoding.UTF8.GetString(ms.ToArray());
            if (!BinanceWebSocketMessageParser.TryParseCombinedStream(json, out var ticker, out var kline))
            {
                continue;
            }

            if (ticker is not null)
            {
                await _tickers.Writer.WriteAsync(ticker, cancellationToken);
            }

            if (kline is not null)
            {
                await _klines.Writer.WriteAsync(kline, cancellationToken);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _supervisorCts?.Cancel();
            if (_supervisor is not null)
            {
                try { await _supervisor; } catch { /* ignore */ }
            }

            if (_socket is { State: WebSocketState.Open })
            {
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "dispose", CancellationToken.None);
            }
        }
        catch
        {
            // Best-effort dispose.
        }
        finally
        {
            _socket?.Dispose();
            _supervisorCts?.Dispose();
        }
    }
}
