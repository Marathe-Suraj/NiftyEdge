using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NiftyEdge.Core.MarketData;
using NiftyEdge.Core.Models;
using NiftyEdge.Core.Repositories;
using NiftyEdge.Core.Signals;

namespace NiftyEdge.Core.Scheduling;

/// <summary>
/// The background heartbeat of the app. Only active during NSE market hours (9:15-15:30 IST,
/// Mon-Fri, minus holidays): polls LTP every tick, and refreshes candles / re-runs strategies at
/// each completed 15-min and 1-hour boundary.
/// </summary>
public class MarketPollingHostedService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MarketPollingHostedService> _logger;
    private readonly Dictionary<TimeFrame, DateTime> _lastCandleBoundaryProcessed = new();

    public MarketPollingHostedService(IServiceScopeFactory scopeFactory, ILogger<MarketPollingHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NiftyEdge market polling service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOneTickAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error during market-polling tick.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Expected on shutdown.
            }
        }
    }

    private async Task RunOneTickAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var provider = scope.ServiceProvider;

        var holidayRepository = provider.GetRequiredService<IMarketHolidayRepository>();
        var holidays = await holidayRepository.GetHolidayDatesAsync(cancellationToken);

        if (!MarketHoursCalculator.IsMarketOpen(DateTime.UtcNow, holidays))
        {
            return;
        }

        var instrumentRepository = provider.GetRequiredService<IInstrumentRepository>();
        var marketDataProvider = provider.GetRequiredService<IMarketDataProvider>();
        var broadcaster = provider.GetRequiredService<ISignalBroadcaster>();
        var outcomeTracker = provider.GetRequiredService<SignalOutcomeTracker>();
        var latestQuoteCache = provider.GetRequiredService<ILatestQuoteCache>();

        // Crypto instruments live in the same table but are served 24/7 by the crypto host off Binance.
        // Polling them here only produces failed Yahoo lookups during NSE hours.
        var instruments = (await instrumentRepository.GetActiveInstrumentsAsync(cancellationToken))
            .Where(i => i.InstrumentType != InstrumentType.CryptoUsdtmFuture)
            .ToList();

        foreach (var instrument in instruments)
        {
            // One instrument's feed hiccup must not abort the tick: candle-boundary strategy
            // evaluation happens after this loop and is far more valuable than a single LTP push.
            try
            {
                var quote = await marketDataProvider.GetLtpAsync(instrument, cancellationToken);
                if (quote is null)
                {
                    _logger.LogWarning("No LTP available for {Symbol} this tick.", instrument.Symbol);
                    continue;
                }

                latestQuoteCache.Store(instrument.InstrumentId, quote);
                await broadcaster.BroadcastPriceUpdateAsync(instrument.InstrumentId, quote, cancellationToken);
                await outcomeTracker.UpdateOutcomesAsync(instrument.InstrumentId, quote.LastTradedPrice, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to process the live price for {Symbol} this tick.", instrument.Symbol);
            }
        }

        var istNow = MarketHoursCalculator.ToIst(DateTime.UtcNow);

        foreach (var timeFrame in new[] { TimeFrame.FifteenMinute, TimeFrame.OneHour })
        {
            var slot = MarketHoursCalculator.FloorToInterval(istNow, timeFrame);
            if (HasNotProcessedBoundary(timeFrame, slot))
            {
                await ProcessCandleBoundaryAsync(provider, instruments, timeFrame, cancellationToken);
                _lastCandleBoundaryProcessed[timeFrame] = slot;
            }
        }
    }

    private async Task ProcessCandleBoundaryAsync(IServiceProvider provider, IReadOnlyList<Instrument> instruments, TimeFrame timeFrame, CancellationToken cancellationToken)
    {
        var marketDataProvider = provider.GetRequiredService<IMarketDataProvider>();
        var candleRepository = provider.GetRequiredService<ICandleRepository>();
        var optionChainRepository = provider.GetRequiredService<IOptionChainRepository>();
        var aggregator = provider.GetRequiredService<SignalAggregatorService>();

        foreach (var instrument in instruments)
        {
            var candles = await marketDataProvider.GetCandlesAsync(instrument, timeFrame, cancellationToken);
            if (candles is null || candles.Count == 0)
            {
                _logger.LogWarning("No candles available for {Symbol} ({TimeFrame}) at boundary.", instrument.Symbol, timeFrame);
                continue;
            }

            await candleRepository.UpsertCandlesAsync(candles, cancellationToken);

            // Strategies key off the final bar's completed OHLC, so the still-forming bar the feed
            // returns has to go before evaluation - otherwise every range/momentum/rejection check is
            // measured against a bar that is only seconds old and can never qualify.
            var completedCandles = CandleSeries.CompletedAsOf(candles, DateTime.UtcNow);
            if (completedCandles.Count == 0)
            {
                _logger.LogWarning("No completed candles for {Symbol} ({TimeFrame}) at boundary.", instrument.Symbol, timeFrame);
                continue;
            }

            var optionChain = await marketDataProvider.GetOptionChainAsync(instrument, cancellationToken);
            if (optionChain is not null)
            {
                await optionChainRepository.SaveSnapshotAsync(optionChain, cancellationToken);
            }

            // 15-minute signals are cross-checked against the concurrent 1-hour trend, and 1-hour signals
            // against a longer look-back window of that same hourly series (TrendConfluenceFilter). Read
            // from the DB rather than making an extra live fetch, since the hourly boundary already
            // persists these candles every hour.
            IReadOnlyList<Candle>? higherTimeframeCandles = timeFrame switch
            {
                TimeFrame.FifteenMinute => await candleRepository.GetRecentCandlesAsync(instrument.InstrumentId, TimeFrame.OneHour, lookbackDays: 5, cancellationToken),
                TimeFrame.OneHour => await candleRepository.GetRecentCandlesAsync(instrument.InstrumentId, TimeFrame.OneHour, lookbackDays: 15, cancellationToken),
                _ => null
            };

            await aggregator.EvaluateAsync(instrument, completedCandles, optionChain, higherTimeframeCandles, cancellationToken);
        }
    }

    private bool HasNotProcessedBoundary(TimeFrame timeFrame, DateTime boundary)
    {
        return !_lastCandleBoundaryProcessed.TryGetValue(timeFrame, out var last) || last != boundary;
    }
}
