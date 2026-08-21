using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NiftyEdge.Core.Alerts;
using NiftyEdge.Core.MarketData;
using NiftyEdge.Core.Models;
using NiftyEdge.Core.Repositories;
using NiftyEdge.Core.Signals;
using NiftyEdge.CryptoTrading.Configuration;
using NiftyEdge.CryptoTrading.Exchanges;
using NiftyEdge.CryptoTrading.Signals;
using NiftyEdge.CryptoTrading.Strategies;

namespace NiftyEdge.CryptoTrading.Hosting;

/// <summary>
/// 24/7 crypto alert loop using free Binance public market data. Does not place exchange orders.
/// REST polling is the authoritative feed because Binance's futures websocket is silently unreachable
/// on some networks; the websocket, when it does deliver, just gets the same work done sooner.
/// </summary>
public sealed class CryptoLiveSignalHostedService : BackgroundService
{
    private static readonly TimeFrame[] TrackedTimeFrames =
        [TimeFrame.FifteenMinute, TimeFrame.OneHour, TimeFrame.FourHour];

    private static readonly TimeSpan WebSocketSilenceTolerance = TimeSpan.FromMinutes(3);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<CryptoOptions> _options;
    private readonly ICryptoRestMarketDataClient _restClient;
    private readonly ICryptoWebSocketClient _webSocketClient;
    private readonly ILogger<CryptoLiveSignalHostedService> _logger;

    private readonly object _evaluationGate = new();
    private readonly Dictionary<int, DateTime> _lastEvaluatedOneHourOpen = new();
    private readonly Dictionary<(int InstrumentId, TimeFrame TimeFrame), DateTime> _lastPersistedClose = new();

    private DateTime? _webSocketStartedUtc;
    private DateTime? _lastWebSocketMessageUtc;
    private bool _webSocketSilenceReported;

    public CryptoLiveSignalHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<CryptoOptions> options,
        ICryptoRestMarketDataClient restClient,
        ICryptoWebSocketClient webSocketClient,
        ILogger<CryptoLiveSignalHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _restClient = restClient;
        _webSocketClient = webSocketClient;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.Enabled)
        {
            _logger.LogInformation("Crypto module disabled via Crypto:Enabled=false.");
            return;
        }

        _logger.LogInformation("Crypto live signal service starting (alert-only, free public data).");

        var symbols = _options.Value.Pairs.Where(p => p.Enabled).Select(p => p.Symbol).ToList();
        if (symbols.Count == 0)
        {
            _logger.LogWarning("No enabled crypto pairs configured.");
            return;
        }

        try
        {
            await WarmupAsync(symbols, stoppingToken);

            var loops = new List<Task> { PollRestAsync(symbols, stoppingToken) };

            if (_options.Value.UseWebSocketStream)
            {
                await _webSocketClient.ConnectAsync(symbols, TrackedTimeFrames, stoppingToken);
                _webSocketStartedUtc = DateTime.UtcNow;
                loops.Add(ProcessTickersAsync(stoppingToken));
                loops.Add(ProcessKlinesAsync(stoppingToken));
            }
            else
            {
                _logger.LogInformation("Crypto websocket disabled; REST polling every {Seconds}s.", _options.Value.RestPollSeconds);
            }

            await Task.WhenAll(loops);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Crypto live signal service failed.");
        }
        finally
        {
            await _webSocketClient.DisposeAsync();
        }
    }

    private async Task WarmupAsync(IReadOnlyList<string> symbols, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var instruments = scope.ServiceProvider.GetRequiredService<IInstrumentRepository>();
        var candles = scope.ServiceProvider.GetRequiredService<ICandleRepository>();
        var active = await instruments.GetActiveInstrumentsAsync(cancellationToken);

        foreach (var symbol in symbols)
        {
            var instrument = active.FirstOrDefault(i => i.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
            if (instrument is null)
            {
                continue;
            }

            foreach (var tf in TrackedTimeFrames)
            {
                try
                {
                    var series = await _restClient.GetKlinesAsync(symbol, tf, limit: 500, cancellationToken: cancellationToken);
                    var closed = ClosedCandleSelector.SelectClosed(series, tf, DateTime.UtcNow, instrument.InstrumentId);
                    if (closed.Count == 0)
                    {
                        continue;
                    }

                    // Deliberately not recording _lastPersistedClose: the first poll then re-reads the
                    // latest close and evaluates it, so a restart can alert without waiting an hour.
                    await candles.UpsertCandlesAsync(closed, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Warm-up failed for {Symbol} {TimeFrame}", symbol, tf);
                }
            }
        }
    }

    private async Task PollRestAsync(IReadOnlyList<string> symbols, CancellationToken cancellationToken)
    {
        var period = TimeSpan.FromSeconds(Math.Max(5, _options.Value.RestPollSeconds));
        using var timer = new PeriodicTimer(period);

        try
        {
            do
            {
                try
                {
                    await PollOnceAsync(symbols, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Crypto REST poll failed; retrying next tick.");
                }
            } while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }

    private async Task PollOnceAsync(IReadOnlyList<string> symbols, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;

        var active = await sp.GetRequiredService<IInstrumentRepository>().GetActiveInstrumentsAsync(cancellationToken);
        var bySymbol = new Dictionary<string, Instrument>(StringComparer.OrdinalIgnoreCase);
        foreach (var instrument in active)
        {
            bySymbol[instrument.Symbol] = instrument;
        }

        await PushLatestPricesAsync(sp, symbols, bySymbol, cancellationToken);

        var candleRepo = sp.GetRequiredService<ICandleRepository>();
        foreach (var symbol in symbols)
        {
            if (!bySymbol.TryGetValue(symbol, out var instrument))
            {
                continue;
            }

            DateTime? newestOneHourClose = null;
            foreach (var tf in TrackedTimeFrames)
            {
                if (!NeedsFetch(instrument.InstrumentId, tf, DateTime.UtcNow))
                {
                    continue;
                }

                var closed = await FetchClosedCandlesAsync(symbol, tf, instrument.InstrumentId, cancellationToken);
                if (closed.Count == 0)
                {
                    continue;
                }

                await candleRepo.UpsertCandlesAsync(closed, cancellationToken);

                var newest = closed.Max(c => c.CandleTime);
                _lastPersistedClose[(instrument.InstrumentId, tf)] = newest;

                if (tf == TimeFrame.OneHour)
                {
                    newestOneHourClose = newest;
                }
            }

            if (newestOneHourClose is not null)
            {
                await EvaluateAndAlertAsync(sp, instrument, newestOneHourClose.Value, cancellationToken);
            }
        }

        ReportWebSocketSilence();
    }

    /// <summary>
    /// Between candle boundaries there is nothing new to fetch, so steady-state polling costs one
    /// price call rather than one klines call per pair per timeframe.
    /// </summary>
    private bool NeedsFetch(int instrumentId, TimeFrame timeFrame, DateTime utcNow)
    {
        var expected = ClosedCandleSelector.MostRecentClosedOpenTime(timeFrame, utcNow);
        return !_lastPersistedClose.TryGetValue((instrumentId, timeFrame), out var have) || have < expected;
    }

    private async Task<IReadOnlyList<Candle>> FetchClosedCandlesAsync(
        string symbol,
        TimeFrame timeFrame,
        int instrumentId,
        CancellationToken cancellationToken)
    {
        var fetched = await _restClient.GetKlinesAsync(symbol, timeFrame, limit: 3, cancellationToken: cancellationToken);
        return ClosedCandleSelector.SelectClosed(fetched, timeFrame, DateTime.UtcNow, instrumentId);
    }

    private async Task PushLatestPricesAsync(
        IServiceProvider sp,
        IReadOnlyList<string> symbols,
        IReadOnlyDictionary<string, Instrument> bySymbol,
        CancellationToken cancellationToken)
    {
        var prices = await _restClient.GetLatestPricesAsync(symbols, cancellationToken);
        if (prices.Count == 0)
        {
            return;
        }

        var broadcaster = sp.GetRequiredService<ISignalBroadcaster>();
        var tracker = sp.GetRequiredService<CryptoOutcomeTracker>();
        var asOf = DateTime.UtcNow;

        foreach (var (symbol, price) in prices)
        {
            if (!bySymbol.TryGetValue(symbol, out var instrument))
            {
                continue;
            }

            await broadcaster.BroadcastPriceUpdateAsync(
                instrument.InstrumentId,
                new LtpQuote
                {
                    LastTradedPrice = price,
                    PreviousClose = price,
                    AsOf = asOf
                },
                cancellationToken);

            await tracker.EvaluateOpenSignalsAsync(symbol, price, asOf, cancellationToken);
        }
    }

    private void ReportWebSocketSilence()
    {
        if (_webSocketSilenceReported || _webSocketStartedUtc is null || !_options.Value.UseWebSocketStream)
        {
            return;
        }

        var lastSeen = _lastWebSocketMessageUtc ?? _webSocketStartedUtc.Value;
        if (DateTime.UtcNow - lastSeen < WebSocketSilenceTolerance)
        {
            return;
        }

        _webSocketSilenceReported = true;
        _logger.LogWarning(
            "No Binance futures websocket data for over {Minutes} minutes even though the socket connected. " +
            "Some networks accept the connection and never push frames. REST polling is covering the feed; " +
            "set Crypto:UseWebSocketStream=false to stop attempting it.",
            WebSocketSilenceTolerance.TotalMinutes);
    }

    private async Task ProcessTickersAsync(CancellationToken cancellationToken)
    {
        await foreach (var ticker in _webSocketClient.StreamTickersAsync(cancellationToken))
        {
            _lastWebSocketMessageUtc = DateTime.UtcNow;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var instruments = await scope.ServiceProvider.GetRequiredService<IInstrumentRepository>()
                    .GetActiveInstrumentsAsync(cancellationToken);
                var instrument = instruments.FirstOrDefault(i =>
                    i.Symbol.Equals(ticker.Symbol, StringComparison.OrdinalIgnoreCase));
                if (instrument is null)
                {
                    continue;
                }

                var broadcaster = scope.ServiceProvider.GetRequiredService<ISignalBroadcaster>();
                await broadcaster.BroadcastPriceUpdateAsync(
                    instrument.InstrumentId,
                    new LtpQuote
                    {
                        LastTradedPrice = ticker.Price,
                        PreviousClose = ticker.Price,
                        AsOf = ticker.EventTimeUtc
                    },
                    cancellationToken);

                var tracker = scope.ServiceProvider.GetRequiredService<CryptoOutcomeTracker>();
                await tracker.EvaluateOpenSignalsAsync(ticker.Symbol, ticker.Price, DateTime.UtcNow, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ticker handling failed for {Symbol}", ticker.Symbol);
            }
        }
    }

    private async Task ProcessKlinesAsync(CancellationToken cancellationToken)
    {
        await foreach (var update in _webSocketClient.StreamKlinesAsync(cancellationToken))
        {
            _lastWebSocketMessageUtc = DateTime.UtcNow;

            if (!update.IsClosed)
            {
                continue;
            }

            try
            {
                await OnClosedCandleAsync(update, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Closed {TimeFrame} candle handling failed for {Symbol}", update.TimeFrame, update.Symbol);
            }
        }
    }

    /// <summary>
    /// Persists every closed candle (15m/1h/4h) so the higher-timeframe bias stays current, then runs
    /// the strategy pipeline on the 1h close only - the timeframe entries are defined on.
    /// </summary>
    private async Task OnClosedCandleAsync(CryptoKlineUpdate update, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;
        var instruments = await sp.GetRequiredService<IInstrumentRepository>().GetActiveInstrumentsAsync(cancellationToken);
        var instrument = instruments.FirstOrDefault(i =>
            i.Symbol.Equals(update.Symbol, StringComparison.OrdinalIgnoreCase));
        if (instrument is null)
        {
            return;
        }

        var candleRepo = sp.GetRequiredService<ICandleRepository>();
        update.Candle.InstrumentId = instrument.InstrumentId;
        update.Candle.TimeFrame = update.TimeFrame;
        await candleRepo.UpsertCandlesAsync(new[] { update.Candle }, cancellationToken);

        if (update.TimeFrame != TimeFrame.OneHour)
        {
            return;
        }

        await EvaluateAndAlertAsync(sp, instrument, update.Candle.CandleTime, cancellationToken);
    }

    /// <summary>
    /// Runs the strategy pipeline for one 1h close and alerts on whatever survives the filters. The
    /// websocket and the REST poller both reach this, so each close is claimed once.
    /// </summary>
    private async Task EvaluateAndAlertAsync(
        IServiceProvider sp,
        Instrument instrument,
        DateTime oneHourCandleOpenUtc,
        CancellationToken cancellationToken)
    {
        if (!TryClaimOneHourClose(instrument.InstrumentId, oneHourCandleOpenUtc))
        {
            return;
        }

        var candleRepo = sp.GetRequiredService<ICandleRepository>();
        var c15 = await candleRepo.GetRecentCandlesAsync(instrument.InstrumentId, TimeFrame.FifteenMinute, 14, cancellationToken);
        var c1h = await candleRepo.GetRecentCandlesAsync(instrument.InstrumentId, TimeFrame.OneHour, 60, cancellationToken);
        var c4h = await candleRepo.GetRecentCandlesAsync(instrument.InstrumentId, TimeFrame.FourHour, 120, cancellationToken);

        _logger.LogDebug(
            "Evaluating {Symbol} on the {CandleTime:u} 1h close (15m={C15}, 1h={C1h}, 4h={C4h} bars).",
            instrument.Symbol, oneHourCandleOpenUtc, c15.Count, c1h.Count, c4h.Count);

        var pipeline = sp.GetRequiredService<CryptoSignalPipeline>();
        var signals = await pipeline.EvaluateAsync(new CryptoStrategyContext
        {
            Instrument = instrument,
            Candles15m = c15,
            Candles1h = c1h,
            Candles4h = c4h
        }, cancellationToken);

        if (signals.Count == 0)
        {
            return;
        }

        var signalRepo = sp.GetRequiredService<ISignalRepository>();
        var alertHistory = sp.GetRequiredService<ICryptoAlertHistoryRepository>();
        var telegram = sp.GetRequiredService<ITelegramAlertSender>();
        var broadcaster = sp.GetRequiredService<ISignalBroadcaster>();
        var threshold = _options.Value.ConfidenceThreshold;

        foreach (var signal in signals)
        {
            var id = await signalRepo.InsertSignalAsync(signal, cancellationToken);
            signal.SignalId = id;
            await broadcaster.BroadcastNewSignalAsync(signal, cancellationToken);

            _logger.LogInformation(
                "Crypto signal {SignalId}: {Strategy} {Direction} {Symbol} @ {Entry} (confidence {Confidence}).",
                id, signal.StrategyName, signal.Direction, signal.InstrumentSymbol, signal.EntryPrice, signal.ConfidenceScore);

            if (signal.ConfidenceScore >= threshold)
            {
                await telegram.SendSignalAlertAsync(signal, cancellationToken);
                await alertHistory.InsertAsync(
                    id,
                    signal.InstrumentSymbol,
                    $"{signal.Direction} {signal.InstrumentSymbol} @ {signal.EntryPrice}",
                    "Telegram",
                    delivered: true,
                    detail: "Sent",
                    cancellationToken);
            }
        }
    }

    private bool TryClaimOneHourClose(int instrumentId, DateTime candleOpenUtc)
    {
        lock (_evaluationGate)
        {
            if (_lastEvaluatedOneHourOpen.TryGetValue(instrumentId, out var last) && last >= candleOpenUtc)
            {
                return false;
            }

            _lastEvaluatedOneHourOpen[instrumentId] = candleOpenUtc;
            return true;
        }
    }
}
