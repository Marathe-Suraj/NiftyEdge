using Microsoft.Extensions.Logging;
using NiftyEdge.Core.Alerts;
using NiftyEdge.Core.Models;
using NiftyEdge.Core.Repositories;
using NiftyEdge.Core.Strategies;

namespace NiftyEdge.Core.Signals;

/// <summary>
/// Runs every registered <see cref="IPriceActionStrategy"/> against an instrument's latest candles,
/// applies the option-chain confirmation filter, de-duplicates against already-open signals,
/// persists the result, pushes it live to the dashboard, and fires a Telegram alert if the
/// confidence score clears the configured threshold.
/// </summary>
public class SignalAggregatorService
{
    private const int DefaultAlertThreshold = 70;

    private readonly IReadOnlyList<IPriceActionStrategy> _strategies;
    private readonly CandleQualityFilter _candleQualityFilter;
    private readonly OptionChainConfirmationFilter _confirmationFilter;
    private readonly TrendConfluenceFilter _trendConfluenceFilter;
    private readonly StrategyQualityFilter _qualityFilter;
    private readonly SessionTimingFilter _sessionTimingFilter;
    private readonly ISignalRepository _signalRepository;
    private readonly ISettingsRepository _settingsRepository;
    private readonly ISignalBroadcaster _broadcaster;
    private readonly ITelegramAlertSender _alertSender;
    private readonly ILogger<SignalAggregatorService> _logger;

    public SignalAggregatorService(
        IEnumerable<IPriceActionStrategy> strategies,
        CandleQualityFilter candleQualityFilter,
        OptionChainConfirmationFilter confirmationFilter,
        TrendConfluenceFilter trendConfluenceFilter,
        StrategyQualityFilter qualityFilter,
        SessionTimingFilter sessionTimingFilter,
        ISignalRepository signalRepository,
        ISettingsRepository settingsRepository,
        ISignalBroadcaster broadcaster,
        ITelegramAlertSender alertSender,
        ILogger<SignalAggregatorService> logger)
    {
        _strategies = strategies.ToList();
        _candleQualityFilter = candleQualityFilter;
        _confirmationFilter = confirmationFilter;
        _trendConfluenceFilter = trendConfluenceFilter;
        _qualityFilter = qualityFilter;
        _sessionTimingFilter = sessionTimingFilter;
        _signalRepository = signalRepository;
        _settingsRepository = settingsRepository;
        _broadcaster = broadcaster;
        _alertSender = alertSender;
        _logger = logger;
    }

    /// <param name="higherTimeframeCandles">
    /// The "higher timeframe" series used by <see cref="TrendConfluenceFilter"/>: the instrument's
    /// 1-hour candles when evaluating a 15-minute signal, or that same 1-hour series itself (a longer
    /// look-back window standing in for a genuine daily trend) when evaluating an hourly signal. Pass
    /// null/empty when unavailable - the filter is a no-op in that case.
    /// </param>
    public async Task EvaluateAsync(
        Instrument instrument, IReadOnlyList<Candle> candles, OptionChainSnapshot? optionChain,
        IReadOnlyList<Candle>? higherTimeframeCandles = null, CancellationToken cancellationToken = default)
    {
        if (candles.Count == 0)
        {
            return;
        }

        foreach (var strategy in _strategies)
        {
            TradeSignal? signal;
            try
            {
                signal = strategy.Evaluate(instrument, candles, optionChain);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Strategy {Strategy} threw while evaluating {Symbol}", strategy.Name, instrument.Symbol);
                continue;
            }

            if (signal is null)
            {
                continue;
            }

            signal = _candleQualityFilter.Apply(signal, candles);
            if (signal is null)
            {
                continue;
            }

            signal = _trendConfluenceFilter.Apply(signal, higherTimeframeCandles);
            if (signal is null)
            {
                continue;
            }

            signal = _qualityFilter.Apply(signal);
            if (signal is null)
            {
                continue;
            }

            signal = _sessionTimingFilter.Apply(signal);
            if (signal is null)
            {
                continue;
            }

            signal = _confirmationFilter.Apply(signal, optionChain);
            if (signal is null)
            {
                continue;
            }

            var duplicate = await _signalRepository.FindOpenDuplicateAsync(instrument.InstrumentId, signal.TimeFrame, signal.Direction, cancellationToken);
            if (duplicate is not null)
            {
                _logger.LogDebug("Skipping duplicate {Direction} signal for {Symbol}; one is already open.", signal.Direction, instrument.Symbol);
                continue;
            }

            await PublishSignalAsync(signal, cancellationToken);
        }
    }

    private async Task PublishSignalAsync(TradeSignal signal, CancellationToken cancellationToken)
    {
        signal.SignalId = await _signalRepository.InsertSignalAsync(signal, cancellationToken);
        await _broadcaster.BroadcastNewSignalAsync(signal, cancellationToken);

        var thresholdSetting = await _settingsRepository.GetSettingAsync(AppSettingKeys.AlertConfidenceThreshold, cancellationToken);
        var threshold = int.TryParse(thresholdSetting, out var parsed) ? parsed : DefaultAlertThreshold;

        _logger.LogInformation("New {Direction} signal for {Symbol} via {Strategy}: entry {Entry}, confidence {Confidence}%",
            signal.Direction, signal.InstrumentSymbol, signal.StrategyName, signal.EntryPrice, signal.ConfidenceScore);

        if (signal.ConfidenceScore >= threshold)
        {
            await _alertSender.SendSignalAlertAsync(signal, cancellationToken);
        }
        else
        {
            _logger.LogInformation(
                "No Telegram alert for {Symbol}: confidence {Confidence}% is below the configured threshold of {Threshold}%.",
                signal.InstrumentSymbol, signal.ConfidenceScore, threshold);
        }
    }
}
